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

    /// <summary>
    /// Encode a string key into a uint span and return as ReadOnlyBitString.
    /// byteCount must be pre-computed via Encoding.UTF8.GetByteCount(key).
    /// keyBuffer must have at least (byteCount + 3) / 4 uints.
    /// </summary>
    private static ReadOnlyBitString EncodeKey(string key, int byteCount, Span<uint> keyBuffer)
    {
        Span<byte> utf8Bytes = stackalloc byte[byteCount];
        Encoding.UTF8.GetBytes(key, utf8Bytes);

        // Pack bytes into uints (4 bytes per uint, LSB-first)
        keyBuffer.Clear();
        for (int i = 0; i < byteCount; i++)
        {
            int uintIndex = i / 4;
            int byteInUint = i % 4;
            keyBuffer[uintIndex] |= (uint)utf8Bytes[i] << (byteInUint * 8);
        }

        return ReadOnlyBitString.Wrap(keyBuffer, byteCount * 8);
    }

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

        int byteCount = Encoding.UTF8.GetByteCount(key);
        int uintCount = (byteCount + 3) / 4;
        Span<uint> keyBuffer = stackalloc uint[uintCount];
        var keyBits = EncodeKey(key, byteCount, keyBuffer);

        return TryReadInternal(ref keyBits, 0, 0, out value);
    }

    private bool TryReadInternal(ref ReadOnlyBitString keyBits, int keyBitIndex, int nodeBitPos, out long value)
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
            var keyView = keyBits.Slice(keyBitIndex, prefixLength);
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
            return TryReadInternal(ref keyBits, keyBitIndex, leftChildPos, out value);
        }
        else
        {
            return TryReadInternal(ref keyBits, keyBitIndex, rightChildPos, out value);
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

        int byteCount = Encoding.UTF8.GetByteCount(key);
        int uintCount = (byteCount + 3) / 4;
        Span<uint> keyBuffer = stackalloc uint[uintCount];
        var keyBits = EncodeKey(key, byteCount, keyBuffer);

        // Check if trie is empty
        var reader = new BitArrayReader(_buffer);
        var (_, _, isDeadEnd) = ReadNodeHeader(ref reader);

        if (isDeadEnd)
        {
            // Empty trie - create root node with this key
            return WriteNewRoot(ref keyBits, value);
        }

        // Track ancestors during descent for size updates
        var ancestors = new List<int>();
        return TryWriteInternal(ref keyBits, 0, 0, value, ancestors);
    }

    private bool WriteNewRoot(ref ReadOnlyBitString keyBits, long value)
    {
        // Create a leaf node with the entire key as prefix
        int prefixLength = keyBits.Length;
        int nodeSize = CalculateNodeSize(true, false, prefixLength);

        if (nodeSize > BufferSizeBits)
            return false;

        var writer = new BitArrayWriter(_buffer);

        // Write the node
        WriteNodeHeader(ref writer, true, false, nodeSize, ref keyBits);
        writer.WriteLong(value);

        return true;
    }

    private bool TryWriteInternal(ref ReadOnlyBitString keyBits, int keyBitIndex, int nodeBitPos, long value, List<int> ancestors)
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
            var keyView = keyBits.Slice(keyBitIndex, bitsToCompare);
            matchedBits = prefixView.CommonPrefixLength(ref keyView);
        }

        // Case 1: Divergence within prefix - need to split
        if (matchedBits < prefixLength && keyBitIndex + matchedBits < keyBits.Length)
        {
            return SplitNode(nodeBitPos, ref keyBits, keyBitIndex, matchedBits, value, ancestors);
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
                return SplitNodeKeyExhausted(nodeBitPos, matchedBits, value, ancestors);
            }
        }

        // Case 3: Prefix fully matched, more key bits remain
        keyBitIndex += prefixLength;
        reader.Seek(prefixStartPos + prefixLength);

        if (!hasChildren)
        {
            // Need to add children to this leaf
            return AddChildToLeaf(nodeBitPos, ref keyBits, keyBitIndex, value, ancestors);
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
                return ReplaceDeadEnd(leftChildPos, ref keyBits, keyBitIndex, value, ancestors);
            return TryWriteInternal(ref keyBits, keyBitIndex, leftChildPos, value, ancestors);
        }
        else
        {
            reader.Seek(rightChildPos);
            (_, _, bool rightIsDeadEnd) = ReadNodeHeader(ref reader);

            if (rightIsDeadEnd)
                return ReplaceDeadEnd(rightChildPos, ref keyBits, keyBitIndex, value, ancestors);
            return TryWriteInternal(ref keyBits, keyBitIndex, rightChildPos, value, ancestors);
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

    // Upgrade an internal node to have a value, shifting bits as needed
    private bool RewriteNodeWithValue(int nodeBitPos, long value, List<int> ancestors)
    {
        // Read current node
        var (hasValue, hasChildren, oldSize, prefixLength) = ReadFullNodeHeader(nodeBitPos, out var reader);
        if (hasValue) throw new InvalidOperationException("Node already has value");
        int delta = 64; // size of value in bits

        // Shift everything after this node
        if (!ShiftBits(nodeBitPos + oldSize, delta))
            return false;

        // Rewrite node with value
        var writer = new BitArrayWriter(_buffer, nodeBitPos);
        writer.WriteBit(true); // HasValue
        writer.WriteBit(hasChildren); //or skip, because unchanged
        WriteSize(ref writer, oldSize + delta);
        writer.Seek(reader.BitPosition + prefixLength); //skip prefix

        // Write the new value
        writer.WriteLong(value);

        // Children were already shifted, and now follow naturally

        UpdateAncestorSizes(ancestors, delta);

        return true;
    }

    // TODO: do not pass ancestors, return delta instead and update sizes when bubbling back up the stack
    private bool SplitNode(int nodeBitPos, ref ReadOnlyBitString keyBits, int keyBitIndex, int matchedBits, long newValue, List<int> ancestors)
    {
        // Read current node completely first
        var (oldHasValue, oldHasChildren, oldSize, oldPrefixLength) = ReadFullNodeHeader(nodeBitPos, out var reader);
        var oldPrefixBits = CopyBitsToBuffer(reader.BitPosition, oldPrefixLength);
        reader.Skip(oldPrefixLength);
        long oldValue = oldHasValue ? reader.ReadLong() : 0;

        // TODO: improve efficiency here by avoiding copying bits to buffer
        // 1. calculate the new prefix size
        // 2. calculate size of new node
        // 3. check space
        // 4. shift bits, don't copy bits to buffer
        // 5. write new node

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
            WriteNewKeyAsChild(ref writer, ref keyBits, keyBitIndex + matchedBits + 1, newRemainingKeyLen, newValue, newChildSize);
        }
        else
        {
            // New content goes left, old content goes right
            WriteNewKeyAsChild(ref writer, ref keyBits, keyBitIndex + matchedBits + 1, newRemainingKeyLen, newValue, newChildSize);
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

    private void WriteNewKeyAsChild(ref BitArrayWriter writer, ref ReadOnlyBitString keyBits, int startIndex, int prefixLength, long value, int totalSize)
    {
        var prefix = keyBits.Slice(startIndex, prefixLength);
        WriteNodeHeader(ref writer, true, false, totalSize, ref prefix);

        // Write value
        writer.WriteLong(value);
    }

    private bool SplitNodeKeyExhausted(int nodeBitPos, int matchedBits, long newValue, List<int> ancestors)
    {
        // The new key ends within the existing prefix.
        // New structure:
        // - This node: prefix = first matchedBits, HasValue = true (new key's value), HasChildren = true
        // - One child contains old content (remaining prefix + old value + old children)
        // - Other child is dead end


        // TODO: improve efficiency here by avoiding copying bits to buffer
        // 1. calculate the new prefix size
        // 2. calculate size of new node
        // 3. check space
        // 4. shift bits, don't copy any bits to buffer
        // 5. write new node
        // If we do it perfectly, we can start the shift exactly where the prefix diverges 
        // so we don't even need to copy the prefix bits


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

    private bool AddChildToLeaf(int nodeBitPos, ref ReadOnlyBitString keyBits, int keyBitIndex, long value, List<int> ancestors)
    {
        // Read current leaf node
        var (hasValue, _, oldSize, prefixLength) = ReadFullNodeHeader(nodeBitPos, out var reader);
        reader.Skip(prefixLength);

        long existingValue = hasValue ? reader.ReadLong() : 0;
        // reader is now at end of node
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
        writer.WriteBit(hasValue);
        writer.WriteBit(true); // HasChildren
        WriteSize(ref writer, newSize);
        writer.Seek(reader.BitPosition); // Skip value and prefix

        // Write children
        if (nextBit == false)
        {
            // New child goes left
            WriteNewKeyAsChild(ref writer, ref keyBits, keyBitIndex, remainingKeyLen, value, newChildSize);
            WriteDeadEnd(ref writer);
        }
        else
        {
            // New child goes right
            WriteDeadEnd(ref writer);
            WriteNewKeyAsChild(ref writer, ref keyBits, keyBitIndex, remainingKeyLen, value, newChildSize);
        }

        UpdateAncestorSizes(ancestors, delta);

        return true;
    }

    private bool ReplaceDeadEnd(int deadEndPos, ref ReadOnlyBitString keyBits, int keyBitIndex, long value, List<int> ancestors)
    {
        int remainingKeyLen = keyBits.Length - keyBitIndex;
        int newNodeSize = CalculateNodeSize(true, false, remainingKeyLen);
        int delta = newNodeSize - DeadEndSize;

        if (UsedBits + delta > BufferSizeBits)
            return false;

        if (!ShiftBits(deadEndPos + DeadEndSize, delta))
            return false;

        var writer = new BitArrayWriter(_buffer, deadEndPos);
        WriteNewKeyAsChild(ref writer, ref keyBits, keyBitIndex, remainingKeyLen, value, newNodeSize);

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
    /// </summary>
    private void ShiftBitsRight(int fromBitPos, int delta)
    {
        int bitsToMove = UsedBits - fromBitPos;
        BitArrayWriter.ShiftBitsRight(_buffer, fromBitPos, bitsToMove, delta);
    }

    /// <summary>
    /// Shift bits left (shrink) by delta bits, starting from fromBitPos.
    /// </summary>
    private void ShiftBitsLeft(int fromBitPos, int delta)
    {
        int bitsToMove = UsedBits - fromBitPos;
        BitArrayWriter.ShiftBitsLeft(_buffer, fromBitPos, bitsToMove, delta);
    }

    private void UpdateAncestorSizes(List<int> ancestors, int delta)
    {
        // Update sizes for all ancestors (collected during descent, before modification)
        if (delta == 0 || ancestors.Count == 0)
            return;

        foreach (int nodePos in ancestors)
        {
            int sizePos = nodePos + 2; // after HasValue and HasChildren bits
            var reader = new BitArrayReader(_buffer, sizePos);

            int currentSize = ReadSize(ref reader);
            int newSize = currentSize + delta;

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

        int byteCount = Encoding.UTF8.GetByteCount(key);
        int uintCount = (byteCount + 3) / 4;
        Span<uint> keyBuffer = stackalloc uint[uintCount];
        var keyBits = EncodeKey(key, byteCount, keyBuffer);

        // Find the node and delete, propagating size changes via return value
        DeleteInternal(ref keyBits, 0, 0);
    }

    /// <summary>
    /// Delete a key from the trie. Returns (success, delta) where delta is the
    /// size change that needs to be propagated to ancestors.
    /// </summary>
    private (bool success, int delta) DeleteInternal(ref ReadOnlyBitString keyBits, int keyBitIndex, int nodeBitPos)
    {
        var reader = new BitArrayReader(_buffer, nodeBitPos);

        var (hasValue, hasChildren, isDeadEnd) = ReadNodeHeader(ref reader);
        if (isDeadEnd)
            return (false, 0); // Dead end

        int sizeFieldPos = reader.BitPosition;
        int oldSize = ReadSize(ref reader);
        int prefixLength = VarInt.Read(ref reader);
        int prefixStartPos = reader.BitPosition;

        // Match prefix
        int keyRemainingBits = keyBits.Length - keyBitIndex;
        int bitsToCompare = Math.Min(prefixLength, keyRemainingBits);

        if (bitsToCompare > 0)
        {
            var prefixView = ReadOnlyBitString.Wrap(_buffer).Slice(prefixStartPos, bitsToCompare);
            var keyView = keyBits.Slice(keyBitIndex, bitsToCompare);
            int matchedBits = prefixView.CommonPrefixLength(ref keyView);

            if (matchedBits < bitsToCompare)
                return (false, 0); // Prefix mismatch
        }

        if (bitsToCompare < prefixLength && keyRemainingBits <= bitsToCompare)
            return (false, 0); // Key exhausted before prefix ended

        reader.Skip(prefixLength);
        keyBitIndex += prefixLength;

        // Key fully matched?
        if (keyBitIndex == keyBits.Length)
        {
            if (!hasValue)
                return (false, 0); // No value to delete

            if (!hasChildren)
            {
                // Leaf node with value - convert to dead end
                var writer = new BitArrayWriter(_buffer, nodeBitPos);
                WriteDeadEnd(ref writer);

                int delta = DeadEndSize - oldSize;
                // Shift everything after this node
                ShiftBits(nodeBitPos + oldSize, delta);
                return (true, delta);
            }

            // Internal node with value and children - remove value, keep children
            // Need to shift children left by 64 bits and update size
            int valuePos = reader.BitPosition;
            int childrenStartPos = valuePos + 64;

            // First shift children left by 64 bits (overwriting the value)
            // Must do this BEFORE updating size, because ShiftBitsLeft uses UsedBits
            ShiftBitsLeft(childrenStartPos, 64);

            // Now update node header: clear HasValue, update size
            var writer2 = new BitArrayWriter(_buffer, nodeBitPos);
            writer2.WriteBit(false); // HasValue = false
            writer2.WriteBit(true);  // HasChildren = true (unchanged)
            WriteSize(ref writer2, oldSize - 64);

            return (true, -64);
        }

        // More key bits - follow children
        if (!hasChildren)
            return (false, 0);

        if (hasValue)
            reader.Skip(64);

        bool nextBit = keyBits[keyBitIndex];
        keyBitIndex++;

        var (leftChildPos, rightChildPos, leftIsDeadEnd) = CalculateChildPositions(ref reader);

        (bool success, int childDelta) = nextBit == false
            ? DeleteInternal(ref keyBits, keyBitIndex, leftChildPos)
            : DeleteInternal(ref keyBits, keyBitIndex, rightChildPos);

        if (!success || childDelta == 0)
            return (success, 0);

        // Child size changed - update this node's size
        var sizeWriter = new BitArrayWriter(_buffer, sizeFieldPos);
        WriteSize(ref sizeWriter, oldSize + childDelta);

        return (true, childDelta);
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
