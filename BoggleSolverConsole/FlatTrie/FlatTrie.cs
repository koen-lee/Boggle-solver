using System.Runtime.CompilerServices;
using System.Text;
using BitUtilities;

namespace FlatTrie;

/// <summary>
/// A trie implementation backed by a fixed 32KB uint[] array.
/// Uses bit-level branching (left=0, right=1) with prefix compression.
/// </summary>
public class FlatTrie : ITrie
{
    /// <summary>
    /// Buffer size in uints (32KB = 8192 uints).
    /// </summary>
    public const int BufferSizeUints = 8192;

    /// <summary>
    /// Buffer size in bits.
    /// </summary>
    public const int BufferSizeBits = BufferSizeUints * 32;

    private readonly uint[] _buffer = new uint[BufferSizeUints];

    /// <summary>
    /// Size of a dead end marker in bits.
    /// </summary>
    private const int DeadEndSize = 2;

    /// <summary>
    /// Compute the used bit count from the root node.
    /// This is the size of the trie root (including header).
    /// </summary>
    private int UsedBits
    {
        get
        {
            var reader = new BitArrayReader(_buffer);
            var (_, _, isDeadEnd) = ReadNodeHeader(ref reader);

            if (isDeadEnd)
                return DeadEndSize;

            return ReadSize(ref reader);
        }
    }

    /// <summary>
    /// Fixed size in bits for node size fields (raw 18 bits).
    /// Max value: 262,143 bits = 32KB, which matches our buffer size.
    /// </summary>
    private const int SizeFieldBits = 18;

    private static void WriteSize(ref BitArrayWriter writer, int value)
        => writer.WriteBits((uint)value, SizeFieldBits);

    private static int ReadSize(ref BitArrayReader reader)
        => (int)reader.ReadBits(SizeFieldBits);

    /// <summary>
    /// Write a dead end marker.
    /// </summary>
    private static void WriteDeadEnd(ref BitArrayWriter writer)
    {
        writer.WriteBits(0, 2); // HasValue=0, HasChildren=0
    }

    /// <summary>
    /// Calculate the total size needed to write a node.
    /// Uses fixed 18-bit size field to avoid rebuilds.
    /// </summary>
    private static int CalculateNodeSize(bool hasValue, bool hasChildren, int prefixLength, int childrenSize = 0)
    {
        if (!hasValue && !hasChildren)
            return DeadEndSize;

        int size = 2; // Flags
        size += SizeFieldBits; // Fixed size field
        size += VarInt.GetEncodedBitCount(prefixLength);
        size += prefixLength;
        if (hasValue) size += 64;
        size += childrenSize;

        return size;
    }

    private static BitString KeyToBits(string key)
        => BitString.FromBytes(Encoding.UTF8.GetBytes(key));

    private static void WriteNodeHeader(ref BitArrayWriter writer, bool hasValue, bool hasChildren, int nodeSize, ref ReadOnlyBitString prefix)
    {
        writer.WriteBit(hasValue);
        writer.WriteBit(hasChildren);
        WriteSize(ref writer, nodeSize);
        VarInt.Write(ref writer, prefix.Length);
        writer.WriteBitString(ref prefix);
    }

    private uint[] CopyBitsToBuffer(int bitPosition, int bitCount)
    {
        var slice = ReadOnlyBitString.Wrap(_buffer).Slice(bitPosition, bitCount);
        var buffer = new uint[(bitCount + 31) / 32];
        var writer = new BitArrayWriter(buffer);
        writer.WriteBitString(ref slice);
        return buffer;
    }

    public FlatTrie()
    {
        // Buffer starts as all zeros, which is a dead end (empty trie)
    }

    public bool TryRead(string key, out long value)
    {
        value = 0;

        if (string.IsNullOrEmpty(key))
            return false;

        return TryReadInternal(KeyToBits(key), 0, 0, out value);
    }

    private bool TryReadInternal(BitString keyBits, int keyBitIndex, int nodeBitPos, out long value)
    {
        value = 0;
        var reader = new BitArrayReader(_buffer, nodeBitPos);

        var (hasValue, hasChildren, isDeadEnd) = ReadNodeHeader(ref reader);

        if (isDeadEnd)
            return false;

        // Read node header
        reader.Skip(SizeFieldBits);
        int prefixLength = VarInt.Read(ref reader);

        // Match prefix bits against key bits
        int keyRemainingBits = keyBits.Length - keyBitIndex;
        if (keyRemainingBits < prefixLength)
        {
            // Key exhausted before prefix - no match
            return false;
        }

        if (prefixLength > 0)
        {
            var prefixView = ReadOnlyBitString.Wrap(_buffer).Slice(reader.BitPosition, prefixLength);
            var keyView = keyBits.Slice(keyBitIndex, prefixLength).AsReadOnly();
            int matchedBits = prefixView.CommonPrefixLength(ref keyView);

            if (matchedBits < prefixLength)
            {
                // Mismatch within prefix
                return false;
            }

            reader.Skip(prefixLength);
            keyBitIndex += prefixLength;
        }

        // All key bits matched?
        if (keyBitIndex == keyBits.Length)
        {
            if (hasValue)
            {
                value = reader.ReadLong();
                return true;
            }
            return false; // Key matches but no value stored here
        }

        // More key bits remain - need to follow children
        if (!hasChildren)
            return false;

        // Skip value if present
        if (hasValue)
        {
            reader.Skip(64);
        }

        // Next key bit determines left (0) or right (1)
        bool nextBit = keyBits[keyBitIndex];
        keyBitIndex++;

        var (leftChildPos, rightChildPos, leftIsDeadEnd) = CalculateChildPositions(ref reader);

        if (nextBit == false)
        {
            if (leftIsDeadEnd)
                return false;
            return TryReadInternal(keyBits, keyBitIndex, leftChildPos, out value);
        }
        else
        {
            return TryReadInternal(keyBits, keyBitIndex, rightChildPos, out value);
        }
    }

    /// <summary>
    /// Read a trie node header: HasValue, HasChildren, and whether it's a dead end.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (bool hasValue, bool hasChildren, bool isDeadEnd) ReadNodeHeader(ref BitArrayReader reader)
    {
        var header = reader.ReadBits(2);
        bool hasValue = (header & 1u) != 0;
        bool hasChildren = (header & 2u) != 0;
        bool isDeadEnd = header == 0;
        return (hasValue, hasChildren, isDeadEnd);
    }

    /// <summary>
    /// Read a full node header: flags, size, prefix length. Reader is left at the prefix start position.
    /// </summary>
    private (bool hasValue, bool hasChildren, int size, int prefixLength) ReadFullNodeHeader(int nodeBitPos, out BitArrayReader reader)
    {
        reader = new BitArrayReader(_buffer, nodeBitPos);
        var (hasValue, hasChildren, isDeadEnd) = ReadNodeHeader(ref reader);
        if (isDeadEnd)
            return (false, false, DeadEndSize, 0);
        int size = ReadSize(ref reader);
        int prefixLength = VarInt.Read(ref reader);
        return (hasValue, hasChildren, size, prefixLength);
    }

    public bool TryWrite(string key, long value)
    {
        if (string.IsNullOrEmpty(key))
            return false;

        var keyBits = KeyToBits(key);

        // Check if trie is empty
        var reader = new BitArrayReader(_buffer);
        var (_, _, isDeadEnd) = ReadNodeHeader(ref reader);

        if (isDeadEnd)
        {
            // Empty trie - create root node with this key
            return WriteNewRoot(keyBits, value);
        }

        // Track ancestors during descent for size updates
        var ancestors = new List<int>();
        return TryWriteInternal(keyBits, 0, 0, value, ancestors);
    }

    private bool WriteNewRoot(BitString keyBits, long value)
    {
        // Create a leaf node with the entire key as prefix
        int prefixLength = keyBits.Length;
        int nodeSize = CalculateNodeSize(true, false, prefixLength);

        if (nodeSize > BufferSizeBits)
            return false;

        var writer = new BitArrayWriter(_buffer);

        // Write the node
        var prefix = keyBits.AsReadOnly();
        WriteNodeHeader(ref writer, true, false, nodeSize, ref prefix);
        writer.WriteLong(value);

        return true;
    }

    private bool TryWriteInternal(BitString keyBits, int keyBitIndex, int nodeBitPos, long value, List<int> ancestors)
    {
        var reader = new BitArrayReader(_buffer, nodeBitPos);

        var (hasValue, hasChildren, isDeadEnd) = ReadNodeHeader(ref reader);

        // Dead end - this shouldn't happen if called correctly
        if (isDeadEnd)
            return false;
        reader.Skip(SizeFieldBits);
        int prefixLength = VarInt.Read(ref reader);
        int prefixStartPos = reader.BitPosition;

        // Match prefix bits using XOR-based comparison
        int keyRemainingBits = keyBits.Length - keyBitIndex;
        int bitsToCompare = Math.Min(prefixLength, keyRemainingBits);
        int matchedBits = 0;

        if (bitsToCompare > 0)
        {
            var prefixView = ReadOnlyBitString.Wrap(_buffer).Slice(prefixStartPos, bitsToCompare);
            var keyView = keyBits.Slice(keyBitIndex, bitsToCompare).AsReadOnly();
            matchedBits = prefixView.CommonPrefixLength(ref keyView);
        }

        // Case 1: Divergence within prefix - need to split
        if (matchedBits < prefixLength && keyBitIndex + matchedBits < keyBits.Length)
        {
            return SplitNode(nodeBitPos, keyBits, keyBitIndex, matchedBits, value, ancestors);
        }

        // Case 2: Key exhausted within or at end of prefix
        if (keyBitIndex + matchedBits == keyBits.Length)
        {
            if (matchedBits == prefixLength)
            {
                if (!hasValue)
                {
                    // Need to expand the node to include a value
                    // This requires shifting and is complex
                    return RewriteNodeWithValue(nodeBitPos, value, ancestors);
                }
                // Exact match - update value
                return UpdateNodeValue(nodeBitPos, value);
            }
            else
            {
                // Key ends in middle of prefix - need to split
                return SplitNodeKeyExhausted(nodeBitPos, keyBits, keyBitIndex, matchedBits, value, ancestors);
            }
        }

        // Case 3: Prefix fully matched, more key bits remain
        keyBitIndex += prefixLength;
        reader.Seek(prefixStartPos + prefixLength);

        if (!hasChildren)
        {
            // Need to add children to this leaf
            return AddChildToLeaf(nodeBitPos, keyBits, keyBitIndex, value, ancestors);
        }

        // Skip value if present
        if (hasValue)
        {
            reader.Skip(64);
        }

        // Add this node to ancestors before descending
        ancestors.Add(nodeBitPos);

        // Follow appropriate child
        bool nextBit = keyBits[keyBitIndex];
        keyBitIndex++;

        var (leftChildPos, rightChildPos, leftIsDeadEnd) = CalculateChildPositions(ref reader);

        if (nextBit == false)
        {
            if (leftIsDeadEnd)
                return ReplaceDeadEnd(leftChildPos, keyBits, keyBitIndex, value, ancestors);
            return TryWriteInternal(keyBits, keyBitIndex, leftChildPos, value, ancestors);
        }
        else
        {
            reader.Seek(rightChildPos);
            (_, _, bool rightIsDeadEnd) = ReadNodeHeader(ref reader);

            if (rightIsDeadEnd)
                return ReplaceDeadEnd(rightChildPos, keyBits, keyBitIndex, value, ancestors);
            return TryWriteInternal(keyBits, keyBitIndex, rightChildPos, value, ancestors);
        }
    }

    private bool UpdateNodeValue(int nodeBitPos, long value)
    {
        // Just update existing value in place
        var reader = new BitArrayReader(_buffer, nodeBitPos);
        reader.ReadBit(); // HasValue
        reader.ReadBit(); // HasChildren
        reader.Skip(SizeFieldBits);
        int prefixLength = VarInt.Read(ref reader);
        reader.Skip(prefixLength); // Skip prefix

        // Now at value position
        var writer = new BitArrayWriter(_buffer, reader.BitPosition);
        writer.WriteLong(value);

        return true;
    }

    private bool RewriteNodeWithValue(int nodeBitPos, long value, List<int> ancestors)
    {
        // Read current node
        var (hasValue, hasChildren, oldSize, prefixLength) = ReadFullNodeHeader(nodeBitPos, out var reader);
        var prefixBits = CopyBitsToBuffer(reader.BitPosition, prefixLength);

        // Calculate new size (adding 64 bits for value)
        int childrenSize = hasChildren ? (oldSize - (reader.BitPosition + prefixLength - nodeBitPos)) : 0;
        int newSize = CalculateNodeSize(true, hasChildren, prefixLength, childrenSize);

        int delta = newSize - oldSize;

        // Shift everything after this node
        if (!ShiftBits(nodeBitPos + oldSize, delta))
            return false;

        // Rewrite node with value
        var writer = new BitArrayWriter(_buffer, nodeBitPos);
        var prefix = ReadOnlyBitString.Wrap(prefixBits, prefixLength);
        WriteNodeHeader(ref writer, true, hasChildren, newSize, ref prefix);

        // Write value
        writer.WriteLong(value);

        // Children were already shifted, and now follow naturally

        UpdateAncestorSizes(ancestors, delta);

        return true;
    }

    private bool SplitNode(int nodeBitPos, BitString keyBits, int keyBitIndex, int matchedBits, long newValue, List<int> ancestors)
    {
        // Read current node completely first
        var (oldHasValue, oldHasChildren, oldSize, oldPrefixLength) = ReadFullNodeHeader(nodeBitPos, out var reader);
        var oldPrefixBits = CopyBitsToBuffer(reader.BitPosition, oldPrefixLength);
        reader.Skip(oldPrefixLength);
        long oldValue = oldHasValue ? reader.ReadLong() : 0;

        // Save children data if present (BEFORE any shifting)
        int childrenStartPos = reader.BitPosition;
        int childrenSize = oldHasChildren ? (oldSize - (childrenStartPos - nodeBitPos)) : 0;
        uint[]? childrenCopy = oldHasChildren && childrenSize > 0
            ? CopyBitsToBuffer(childrenStartPos, childrenSize)
            : null;

        // The diverging bits
        var oldPrefixReader = new BitArrayReader(oldPrefixBits, matchedBits);
        bool oldDivergeBit = oldPrefixReader.ReadBit();

        // Old node's remaining prefix (after the diverge bit)
        int oldRemainingPrefixLen = oldPrefixLength - matchedBits - 1;

        // New key's remaining bits (after the diverge bit)
        int newRemainingKeyLen = keyBits.Length - keyBitIndex - matchedBits - 1;

        // Calculate sizes
        int oldChildSize = CalculateNodeSize(oldHasValue, oldHasChildren, oldRemainingPrefixLen, childrenSize);
        int newChildSize = CalculateNodeSize(true, false, newRemainingKeyLen);

        int newParentSize = CalculateNodeSize(false, true, matchedBits, oldChildSize + newChildSize);
        int delta = newParentSize - oldSize;

        // Check space
        if (UsedBits + delta > BufferSizeBits)
            return false;

        // Shift bits after old node
        if (!ShiftBits(nodeBitPos + oldSize, delta))
            return false;

        // Write new parent node
        var writer = new BitArrayWriter(_buffer, nodeBitPos);
        var matchedPrefix = ReadOnlyBitString.Wrap(oldPrefixBits).Slice(0, matchedBits);
        WriteNodeHeader(ref writer, false, true, newParentSize, ref matchedPrefix);

        // Write children in order (left then right)
        if (oldDivergeBit == false)
        {
            // Old content goes left, new content goes right
            WriteOldContentAsChild(ref writer, oldHasValue, oldHasChildren, oldPrefixBits, matchedBits + 1,
                oldRemainingPrefixLen, oldValue, childrenCopy, childrenSize, oldChildSize);
            WriteNewKeyAsChild(ref writer, keyBits, keyBitIndex + matchedBits + 1, newRemainingKeyLen, newValue, newChildSize);
        }
        else
        {
            // New content goes left, old content goes right
            WriteNewKeyAsChild(ref writer, keyBits, keyBitIndex + matchedBits + 1, newRemainingKeyLen, newValue, newChildSize);
            WriteOldContentAsChild(ref writer, oldHasValue, oldHasChildren, oldPrefixBits, matchedBits + 1,
                oldRemainingPrefixLen, oldValue, childrenCopy, childrenSize, oldChildSize);
        }

        UpdateAncestorSizes(ancestors, delta);

        return true;
    }

    private void WriteOldContentAsChild(ref BitArrayWriter writer, bool hasValue, bool hasChildren,
        uint[] prefixBits, int prefixStartBit, int prefixLength, long value,
        uint[]? childrenData, int childrenSize, int totalSize)
    {
        var prefix = ReadOnlyBitString.Wrap(prefixBits).Slice(prefixStartBit, prefixLength);
        WriteNodeHeader(ref writer, hasValue, hasChildren, totalSize, ref prefix);

        if (hasValue)
            writer.WriteLong(value);

        // Copy children data if present
        if (hasChildren && childrenData != null && childrenSize > 0)
        {
            var children = ReadOnlyBitString.Wrap(childrenData, childrenSize);
            writer.WriteBitString(ref children);
        }
    }

    private void WriteNewKeyAsChild(ref BitArrayWriter writer, BitString keyBits, int startIndex, int prefixLength, long value, int totalSize)
    {
        var prefix = keyBits.Slice(startIndex, prefixLength).AsReadOnly();
        WriteNodeHeader(ref writer, true, false, totalSize, ref prefix);

        // Write value
        writer.WriteLong(value);
    }

    private bool SplitNodeKeyExhausted(int nodeBitPos, BitString keyBits, int keyBitIndex, int matchedBits, long newValue, List<int> ancestors)
    {
        // The new key ends within the existing prefix.
        // New structure:
        // - This node: prefix = first matchedBits, HasValue = true (new key's value), HasChildren = true
        // - One child contains old content (remaining prefix + old value + old children)
        // - Other child is dead end

        // Read current node
        var (oldHasValue, oldHasChildren, oldSize, oldPrefixLength) = ReadFullNodeHeader(nodeBitPos, out var reader);
        var oldPrefixBits = CopyBitsToBuffer(reader.BitPosition, oldPrefixLength);
        reader.Skip(oldPrefixLength);
        long oldValue = oldHasValue ? reader.ReadLong() : 0;

        // Save children data if present
        int childrenStartPos = reader.BitPosition;
        int childrenSize = oldHasChildren ? (oldSize - (childrenStartPos - nodeBitPos)) : 0;
        uint[]? childrenCopy = oldHasChildren && childrenSize > 0
            ? CopyBitsToBuffer(childrenStartPos, childrenSize)
            : null;

        // The bit after the matched portion determines which child branch
        var oldPrefixReader = new BitArrayReader(oldPrefixBits, matchedBits);
        bool oldNextBit = oldPrefixReader.ReadBit();

        // Old node's remaining prefix (after the branch bit)
        int oldRemainingPrefixLen = oldPrefixLength - matchedBits - 1;

        // Calculate sizes
        int oldChildSize = CalculateNodeSize(oldHasValue, oldHasChildren, oldRemainingPrefixLen, childrenSize);

        // New parent: HasValue=true, HasChildren=true, one child is old content, other is dead end
        int newParentChildrenSize = oldChildSize + DeadEndSize;
        int newParentSize = CalculateNodeSize(true, true, matchedBits, newParentChildrenSize);
        int delta = newParentSize - oldSize;

        // Check space
        if (UsedBits + delta > BufferSizeBits)
            return false;

        // Shift bits after old node
        if (!ShiftBits(nodeBitPos + oldSize, delta))
            return false;

        // Write new parent node
        var writer = new BitArrayWriter(_buffer, nodeBitPos);
        var matchedPrefix = ReadOnlyBitString.Wrap(oldPrefixBits).Slice(0, matchedBits);
        WriteNodeHeader(ref writer, true, true, newParentSize, ref matchedPrefix);

        // Write new value
        writer.WriteLong(newValue);

        // Write children: old content on one side, dead end on other
        if (oldNextBit == false)
        {
            // Old content goes left
            WriteOldContentAsChild(ref writer, oldHasValue, oldHasChildren, oldPrefixBits, matchedBits + 1,
                oldRemainingPrefixLen, oldValue, childrenCopy, childrenSize, oldChildSize);
            WriteDeadEnd(ref writer);
        }
        else
        {
            // Old content goes right
            WriteDeadEnd(ref writer);
            WriteOldContentAsChild(ref writer, oldHasValue, oldHasChildren, oldPrefixBits, matchedBits + 1,
                oldRemainingPrefixLen, oldValue, childrenCopy, childrenSize, oldChildSize);
        }

        UpdateAncestorSizes(ancestors, delta);

        return true;
    }

    private bool AddChildToLeaf(int nodeBitPos, BitString keyBits, int keyBitIndex, long value, List<int> ancestors)
    {
        // Read current leaf node
        var (hasValue, _, oldSize, prefixLength) = ReadFullNodeHeader(nodeBitPos, out var reader);
        var prefixBits = CopyBitsToBuffer(reader.BitPosition, prefixLength);
        reader.Skip(prefixLength);

        long existingValue = hasValue ? reader.ReadLong() : 0;

        // The next bit determines which child
        bool nextBit = keyBits[keyBitIndex];
        keyBitIndex++;

        int remainingKeyLen = keyBits.Length - keyBitIndex;
        int newChildSize = CalculateNodeSize(true, false, remainingKeyLen);

        // New structure: this node gets HasChildren=true, with one real child and one dead end
        int childrenSize = newChildSize + DeadEndSize;
        int newSize = CalculateNodeSize(hasValue, true, prefixLength, childrenSize);
        int delta = newSize - oldSize;

        if (UsedBits + delta > BufferSizeBits)
            return false;

        if (!ShiftBits(nodeBitPos + oldSize, delta))
            return false;

        // Rewrite node
        var writer = new BitArrayWriter(_buffer, nodeBitPos);
        var prefix = ReadOnlyBitString.Wrap(prefixBits, prefixLength);
        WriteNodeHeader(ref writer, hasValue, true, newSize, ref prefix);

        if (hasValue)
        {
            writer.WriteLong(existingValue);
        }

        // Write children
        if (nextBit == false)
        {
            // New child goes left
            WriteNewKeyAsChild(ref writer, keyBits, keyBitIndex, remainingKeyLen, value, newChildSize);
            WriteDeadEnd(ref writer);
        }
        else
        {
            // New child goes right
            WriteDeadEnd(ref writer);
            WriteNewKeyAsChild(ref writer, keyBits, keyBitIndex, remainingKeyLen, value, newChildSize);
        }

        UpdateAncestorSizes(ancestors, delta);

        return true;
    }

    private bool ReplaceDeadEnd(int deadEndPos, BitString keyBits, int keyBitIndex, long value, List<int> ancestors)
    {
        int remainingKeyLen = keyBits.Length - keyBitIndex;
        int newNodeSize = CalculateNodeSize(true, false, remainingKeyLen);
        int delta = newNodeSize - DeadEndSize;

        if (UsedBits + delta > BufferSizeBits)
            return false;

        if (!ShiftBits(deadEndPos + DeadEndSize, delta))
            return false;

        var writer = new BitArrayWriter(_buffer, deadEndPos);
        WriteNewKeyAsChild(ref writer, keyBits, keyBitIndex, remainingKeyLen, value, newNodeSize);

        UpdateAncestorSizes(ancestors, delta);

        return true;
    }

    private bool ShiftBits(int fromBitPos, int delta)
    {
        if (delta == 0)
            return true;

        if (delta > 0)
        {
            // Expanding - shift right
            if (UsedBits + delta > BufferSizeBits)
                return false;

            ShiftBitsRight(fromBitPos, delta);
        }
        else
        {
            // Shrinking - shift left
            ShiftBitsLeft(fromBitPos, -delta);
        }

        return true;
    }

    /// <summary>
    /// Shift bits right (expand) by delta bits, starting from fromBitPos.
    /// Uses word-level barrel shift for efficiency.
    /// Works backwards from end to avoid overwriting source data.
    /// </summary>
    private void ShiftBitsRight(int fromBitPos, int delta)
    {
        int bitsToMove = UsedBits - fromBitPos;
        if (bitsToMove <= 0)
            return;

        // The rotation amount within a word (0-31)
        int rot = delta & 31;  // delta % 32
        int wordShift = delta >> 5;  // delta / 32

        // Calculate source and destination word ranges
        int usedBits = UsedBits;
        int srcStartWord = fromBitPos >> 5;
        int srcEndWord = (usedBits - 1) >> 5;
        int dstEndWord = (usedBits - 1 + delta) >> 5;

        if (rot == 0)
        {
            // Word-aligned shift - simple word copy backwards
            for (int srcWord = srcEndWord; srcWord >= srcStartWord; srcWord--)
            {
                _buffer[srcWord + wordShift] = _buffer[srcWord];
            }
        }
        else
        {
            // Non-aligned shift - each dest word gets bits from two source words
            // dst[i] = (src[i-wordShift] << rot) | (src[i-wordShift-1] >> (32-rot))

            // Process from end to start
            for (int dstWord = dstEndWord; dstWord >= srcStartWord + wordShift; dstWord--)
            {
                int srcWordHigh = dstWord - wordShift;
                int srcWordLow = srcWordHigh - 1;

                uint highBits = (srcWordHigh >= 0 && srcWordHigh < BufferSizeUints) ? _buffer[srcWordHigh] : 0;
                uint lowBits = (srcWordLow >= 0 && srcWordLow < BufferSizeUints) ? _buffer[srcWordLow] : 0;

                // Combine: upper bits from highBits shifted left, lower bits from lowBits shifted right
                _buffer[dstWord] = (highBits << rot) | (lowBits >> (32 - rot));
            }
        }

        // Clear the gap between old position and new position
        // (the bits that were shifted away from the start)
        int gapStartBit = fromBitPos;
        int gapEndBit = fromBitPos + delta;
        int gapStartWord = gapStartBit >> 5;
        int gapEndWord = (gapEndBit - 1) >> 5;

        // Clear words in the gap
        for (int w = gapStartWord; w <= gapEndWord && w < srcStartWord + wordShift; w++)
        {
            if (w == gapStartWord && (gapStartBit & 31) != 0)
            {
                // Partial clear at start
                uint mask = ~((1u << (gapStartBit & 31)) - 1);  // Clear upper bits
                _buffer[w] &= ~mask;
            }
            else if (w == gapEndWord && (gapEndBit & 31) != 0)
            {
                // Partial clear at end
                uint mask = (1u << (gapEndBit & 31)) - 1;  // Clear lower bits
                _buffer[w] &= ~mask;
            }
            else
            {
                _buffer[w] = 0;
            }
        }
    }

    /// <summary>
    /// Shift bits left (shrink) by delta bits, starting from fromBitPos.
    /// Uses word-level operations for efficiency.
    /// Works forwards from start to end.
    /// </summary>
    private void ShiftBitsLeft(int fromBitPos, int delta)
    {
        int usedBits = UsedBits;
        int bitsToMove = usedBits - fromBitPos;
        if (bitsToMove <= 0)
            return;

        int dstStartBit = fromBitPos - delta;
        int srcEndBit = usedBits;

        // The rotation amount within a word (0-31)
        int rot = delta & 31;
        int wordShift = delta >> 5;

        int srcStartWord = fromBitPos >> 5;
        int srcEndWord = (srcEndBit - 1) >> 5;
        int dstStartWord = dstStartBit >> 5;

        if (rot == 0)
        {
            // Word-aligned shift - simple word copy forwards
            for (int srcWord = srcStartWord; srcWord <= srcEndWord; srcWord++)
            {
                int dstWord = srcWord - wordShift;
                if (dstWord >= 0)
                {
                    _buffer[dstWord] = _buffer[srcWord];
                }
            }
        }
        else
        {
            // Non-aligned shift
            // dst[i] = (src[i+wordShift] >> rot) | (src[i+wordShift+1] << (32-rot))

            for (int dstWord = dstStartWord; dstWord <= srcEndWord - wordShift; dstWord++)
            {
                int srcWordLow = dstWord + wordShift;
                int srcWordHigh = srcWordLow + 1;

                uint lowBits = (srcWordLow >= 0 && srcWordLow < BufferSizeUints) ? _buffer[srcWordLow] : 0;
                uint highBits = (srcWordHigh >= 0 && srcWordHigh < BufferSizeUints) ? _buffer[srcWordHigh] : 0;

                _buffer[dstWord] = (lowBits >> rot) | (highBits << (32 - rot));
            }
        }
    }

    private void UpdateAncestorSizes(List<int> ancestors, int delta)
    {
        // Update sizes for all ancestors (collected during descent, before modification)
        if (delta == 0 || ancestors.Count == 0)
            return;

        foreach (int nodePos in ancestors)
        {
            var reader = new BitArrayReader(_buffer, nodePos);
            reader.ReadBit(); // HasValue
            reader.ReadBit(); // HasChildren

            int sizePos = reader.BitPosition;
            int currentSize = ReadSize(ref reader);
            int newSize = currentSize + delta;

            // Since we always use fixed 18-bit encoding,
            // updates always fit in place - no rebuild needed
            var writer = new BitArrayWriter(_buffer, sizePos);
            WriteSize(ref writer, newSize);
        }
    }

    /// <summary>
    /// Calculate child positions by reading the left child header to determine its size.
    /// </summary>
    private (int leftPos, int rightPos, bool leftIsDead) CalculateChildPositions(ref BitArrayReader reader)
    {
        int leftChildPos = reader.BitPosition;
        var (_, _, leftIsDeadEnd) = ReadNodeHeader(ref reader);

        int rightChildPos = leftIsDeadEnd
            ? leftChildPos + DeadEndSize
            : leftChildPos + ReadSize(ref reader);

        return (leftChildPos, rightChildPos, leftIsDeadEnd);
    }

    public void Delete(string key)
    {
        if (string.IsNullOrEmpty(key))
            return;

        // Find the node and clear HasValue
        DeleteInternal(KeyToBits(key), 0, 0);
    }

    private bool DeleteInternal(BitString keyBits, int keyBitIndex, int nodeBitPos)
    {
        var reader = new BitArrayReader(_buffer, nodeBitPos);

        var (hasValue, hasChildren, isDeadEnd) = ReadNodeHeader(ref reader);
        if (isDeadEnd)
            return false; // Dead end
        reader.Skip(SizeFieldBits);
        int prefixLength = VarInt.Read(ref reader);

        // Match prefix using XOR-based comparison
        int keyRemainingBits = keyBits.Length - keyBitIndex;
        if (keyRemainingBits < prefixLength)
            return false;

        if (prefixLength > 0)
        {
            var prefixView = ReadOnlyBitString.Wrap(_buffer).Slice(reader.BitPosition, prefixLength);
            var keyView = keyBits.Slice(keyBitIndex, prefixLength).AsReadOnly();
            int matchedBits = prefixView.CommonPrefixLength(ref keyView);

            if (matchedBits < prefixLength)
                return false;

            reader.Skip(prefixLength);
            keyBitIndex += prefixLength;
        }

        // Key fully matched?
        if (keyBitIndex == keyBits.Length)
        {
            if (hasValue)
            {
                // Clear HasValue bit
                var writer = new BitArrayWriter(_buffer, nodeBitPos);
                writer.WriteBit(false); // Clear HasValue
                return true;
            }
            return false;
        }

        // More key bits - follow children
        if (!hasChildren)
            return false;

        if (hasValue)
            reader.Skip(64);

        bool nextBit = keyBits[keyBitIndex];
        keyBitIndex++;

        var (leftChildPos, rightChildPos, _) = CalculateChildPositions(ref reader);

        if (nextBit == false)
            return DeleteInternal(keyBits, keyBitIndex, leftChildPos);
        else
            return DeleteInternal(keyBits, keyBitIndex, rightChildPos);
    }

    public void Save(string filename)
    {
        using var stream = File.Create(filename);
        // Convert uint[] to byte[] for writing
        byte[] bytes = new byte[BufferSizeUints * 4];
        Buffer.BlockCopy(_buffer, 0, bytes, 0, bytes.Length);
        stream.Write(bytes);
    }

    public void Load(string filename)
    {
        using var stream = File.OpenRead(filename);
        byte[] bytes = new byte[BufferSizeUints * 4];
        int read = stream.Read(bytes, 0, bytes.Length);
        if (read == bytes.Length)
        {
            Buffer.BlockCopy(bytes, 0, _buffer, 0, bytes.Length);
        }

        // UsedBits is computed from the buffer, no need to recalculate
    }

    /// <summary>
    /// Get statistics about the trie for profiling.
    /// </summary>
    public TrieStats GetStats()
    {
        var stats = new TrieStats();
        CollectStats(0, stats);
        return stats;
    }

    private void CollectStats(int nodeBitPos, TrieStats stats)
    {
        var reader = new BitArrayReader(_buffer, nodeBitPos);

        var (hasValue, hasChildren, isDeadEnd) = ReadNodeHeader(ref reader);

        if (isDeadEnd)
        {
            stats.DeadEndCount++;
            return;
        }

        stats.NodeCount++;
        reader.Skip(SizeFieldBits);
        int prefixLength = VarInt.Read(ref reader);

        stats.TotalPrefixBits += prefixLength;
        if (prefixLength > stats.MaxPrefixLength)
            stats.MaxPrefixLength = prefixLength;

        if (!stats.PrefixLengthHistogram.ContainsKey(prefixLength))
            stats.PrefixLengthHistogram[prefixLength] = 0;
        stats.PrefixLengthHistogram[prefixLength]++;

        if (hasValue)
            stats.ValueCount++;

        if (hasChildren)
        {
            reader.Skip(prefixLength);
            if (hasValue)
                reader.Skip(64);

            var (leftChildPos, rightChildPos, _) = CalculateChildPositions(ref reader);

            CollectStats(leftChildPos, stats);
            CollectStats(rightChildPos, stats);
        }
    }
}

public class TrieStats
{
    public int NodeCount { get; set; }
    public int DeadEndCount { get; set; }
    public int ValueCount { get; set; }
    public int TotalPrefixBits { get; set; }
    public int MaxPrefixLength { get; set; }
    public Dictionary<int, int> PrefixLengthHistogram { get; } = new();
}
