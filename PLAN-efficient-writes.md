# Plan: Efficient Writes for FlatTrie

## Results Summary

| Metric | Before | After | Improvement |
|--------|--------|-------|-------------|
| Ascending order fill time | 22,175ms | 47ms | **472x faster** |
| Random order fill time | (estimated >30s) | 37ms | **~800x faster** |
| Keys capacity | 2,340 | 2,080 | -11% (trade-off) |
| Rebuilds triggered | 1,169 | 0 | Eliminated |

**Root cause identified**: VarInt size field overflows triggered 1,169 full trie rebuilds.
**Solution implemented**: Fixed 20-bit size fields eliminate all rebuilds.

---

## Original Problem Analysis

Original write performance: **22,175ms for 2,340 keys** (~9.5ms per write)
Original read performance: **8ms for 2,340 keys** (~0.003ms per read)

The ~3000x discrepancy is caused by:

1. **Bit-by-bit shifting** in `ShiftBits()` (lines 756-802): Every modification shifts all bits after the modified node one at a time
2. **Full node rewrites** in `SplitNode`, `AddChildToLeaf`, etc.: Read entire node to temp buffers, shift, rewrite completely
3. **Cascading ancestor updates** via `UpdateAncestorSizes()`: Updates each ancestor's size field, triggers full rebuild if VarInt class changes

### Key Insight: Ascending Order Optimization

When inserting keys in ascending order (lexicographic = ascending bit order for UTF-8):
- New keys are always appended to the **rightmost path**
- Left subtrees are **never modified** after creation
- Only the rightmost ancestors need size updates
- Shifts could be **append-only** in many cases

## Proposed Solution: Lazy In-Memory Model with Batched Shifts

### Phase 1: Track Node Metadata Lazily

```
┌─────────────────────────────────────────────────────────────────┐
│  FlatTrieNodeRef - Lightweight reference to a node              │
│  ─────────────────────────────────────────────────────────────  │
│  • InitialBitPosition: where node starts in backing store       │
│  • OriginalSize: node size at reference creation                │
│  • SizeDelta: accumulated size change (children grew)           │
│  • IsDirty: header/prefix/value changed                         │
│  • Parent: reference to parent node                             │
│  • IsRightChild: position under parent                          │
└─────────────────────────────────────────────────────────────────┘
```

Instead of materializing the entire trie, maintain:
- A **path cache** from root to current write position
- Node references are created only for visited nodes
- Dirty tracking propagates upward

### Phase 2: Compute Shift Regions

Before any actual shifting, compute **shift regions**:

```
ShiftRegion {
    StartBit: int       // First bit of region
    EndBit: int         // Last bit of region (exclusive)
    Delta: int          // How much to shift this region
}
```

Algorithm:
1. Walk from modified node up to root
2. For each ancestor with size change:
   - If VarInt class doesn't change: update size in-place (no shift)
   - If VarInt class changes: add a shift region for everything after the size field
3. Merge overlapping/adjacent shift regions with same delta
4. **Optimize**: If rightmost path only, shifts become appends (no actual data movement)

### Phase 3: Apply Shifts in Single Pass

Instead of bit-by-bit:

```csharp
// Current O(n) per bit:
for (int i = _usedBits - 1; i >= fromBitPos; i--)
    // copy one bit

// Proposed O(n/32) using word-level operations:
void ShiftBitsWordAligned(int fromBitPos, int delta)
{
    // 1. Handle unaligned prefix (0-31 bits)
    // 2. Bulk copy aligned words using Array.Copy or Buffer.BlockCopy
    // 3. Handle unaligned suffix (0-31 bits)
}
```

For ascending insertions where we only append:
```csharp
// No shift needed - just write at _usedBits
if (isAppendOnly)
{
    WriteNewNodeAt(_usedBits);
    _usedBits += newNodeSize;
}
```

### Phase 4: Deferred Size Updates

Currently `UpdateAncestorSizes` is eager and triggers rebuild on overflow.

New approach:
```csharp
class PendingSizeUpdate {
    int NodeBitPos;
    int OldSize;
    int NewSize;
    int OldVarIntClass;
    int NewVarIntClass;
}

// Collect all updates
List<PendingSizeUpdate> updates = CollectSizeUpdates(ancestors, delta);

// Check if any need VarInt class promotion
bool needsRestructure = updates.Any(u => u.NewVarIntClass > u.OldVarIntClass);

if (needsRestructure)
{
    // Only restructure affected path, not entire trie
    RestructurePath(updates);
}
else
{
    // All sizes fit - update in place
    foreach (var u in updates)
        UpdateSizeInPlace(u);
}
```

## Implementation Phases

### Phase 1: Word-aligned bulk shifting (High Impact, Medium Effort)
**Goal**: Replace bit-by-bit shifting with word-level operations

1. Add `ShiftBitsWordAligned(int fromBitPos, int delta)` method
2. Handle edge cases for unaligned start/end positions
3. Use `Buffer.BlockCopy` for aligned middle sections
4. Expected improvement: **~32x faster shifts**

### Phase 2: Append-only fast path (High Impact, Low Effort)
**Goal**: Detect and optimize rightmost insertions

1. Track if current write path is rightmost (all right-child navigations)
2. When appending to rightmost:
   - Skip shifting entirely
   - Write new node at `_usedBits`
   - Update ancestor sizes (still in-place)
3. Expected improvement: **O(1) for ascending order inserts**

### Phase 3: Lazy node references with dirty tracking (Medium Impact, High Effort)
**Goal**: Only rewrite what's necessary

1. Extend `FlatTrieNode` class to be a mutable reference
2. Add parent/child navigation without reading children
3. Track `SizeDelta` instead of immediate size recalculation
4. Batch all writes to backing store at end of operation
5. Expected improvement: **Eliminates redundant reads/writes**

### Phase 4: Smart VarInt class management (Medium Impact, Medium Effort)
**Goal**: Avoid full rebuilds when size field overflows

1. Pre-allocate size fields with higher VarInt class when:
   - Node is on a frequently-modified path
   - Node's children are growing
2. When size overflows:
   - Only restructure the affected subtree
   - Use "tunnel" technique: shift only from overflow point to next ancestor
3. Expected improvement: **Eliminates full trie rebuilds**

## Data Structures

### Enhanced FlatTrieNode

```csharp
public class FlatTrieNode
{
    // Existing
    public int InitialBitPosition { get; init; }
    public int OriginalSize { get; private set; }
    public bool IsDirty { get; private set; }

    // New
    public int SizeDelta { get; set; } = 0;  // Accumulated delta from descendants
    public int ComputedBitPosition { get; set; }  // Position after shifts applied
    public FlatTrieNode? Parent { get; set; }
    public bool IsRightChild { get; set; }
    public bool IsOnRightmostPath { get; set; }

    // Lazy children - only created when accessed
    private FlatTrieNode? _leftChild;
    private FlatTrieNode? _rightChild;
    private bool _childrenResolved;

    public int FinalSize => OriginalSize + SizeDelta;
}
```

### Write Context

```csharp
class WriteContext
{
    public List<FlatTrieNode> AncestorPath { get; } = new();
    public bool IsRightmostPath { get; set; } = true;
    public List<ShiftRegion> PendingShifts { get; } = new();

    public void RecordShift(int start, int end, int delta) { ... }
    public void MergeAndApplyShifts(uint[] buffer, ref int usedBits) { ... }
}
```

## Complexity Analysis

| Operation | Current | Phase 1 | Phase 2 (rightmost) | Phase 3+4 |
|-----------|---------|---------|---------------------|-----------|
| Insert    | O(n)    | O(n/32) | O(log k)            | O(log k)  |
| Shift     | O(n)    | O(n/32) | O(1)                | O(1)      |
| Size update| O(k)   | O(k)    | O(k)                | O(k) amortized |

Where:
- n = total bits in trie
- k = depth of tree (number of ancestors)

## Testing Strategy

1. **Correctness**: Existing tests must pass after each phase
2. **Performance benchmark**: Track ms/insert for:
   - Ascending order (best case)
   - Descending order (worst case for rightmost optimization)
   - Random order (average case)
3. **Memory validation**: Verify buffer contents match expected layout

## Open Questions

1. ~~Should we pre-size VarInt fields more aggressively?~~ **ANSWERED**: Yes, fixed 20-bit sizes eliminate rebuilds entirely.
2. Should phase 3 use a separate "write transaction" that batches multiple inserts?
3. Is it worth implementing a "compact" operation that rebuilds the trie optimally after bulk inserts?

## What Was Actually Implemented

### Fixed-Size VarInt for Node Sizes (High Impact, Low Effort)

The investigation revealed that the real bottleneck was **not** bit shifting, but the `RebuildIfStale()` function being triggered 1,169 times for 2,340 keys (50% of writes!).

Each rebuild:
1. Collects all entries via full tree traversal
2. Clears the buffer
3. Reinserts all entries from scratch

This O(n²) behavior dominated all other costs.

**Solution**: Always use VarInt class 3 (20 bits) for size fields instead of variable encoding.

Changes made:
1. Added `VarInt.SizeFieldBits = 20` and `VarInt.WriteSize()` method
2. Updated `FlatTrieNode.CalculateNodeSize()` to use fixed 20-bit size
3. Updated all size writes in `FlatTrie.cs` to use `VarInt.WriteSize()`
4. Simplified `UpdateAncestorSizes()` - no more class overflow checks needed

Trade-off: ~10-16 extra bits per node reduces capacity by ~11%, but writes are 500x+ faster.

### Word-Aligned Bulk Shifting (Implemented but minimal impact)

Also implemented word-level barrel shift operations in `ShiftBitsRight()` and `ShiftBitsLeft()`, but this had negligible impact because the rebuild overhead dominated.
