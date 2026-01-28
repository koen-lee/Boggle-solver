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
    /// Track the used bit count for efficient append operations.
    /// This is recomputed on Load and updated on Write.
    /// </summary>
    private int _usedBits = 0;

    /// <summary>
    /// Flag indicating sizes may be stale and need recalculation.
    /// </summary>
    private bool _sizesStale = false;

    public FlatTrie()
    {
        // Buffer starts as all zeros, which is a dead end (empty trie)
    }

    public bool TryRead(string key, out long value)
    {
        value = 0;

        if (string.IsNullOrEmpty(key))
            return false;

        RebuildIfStale();

        // Convert key to bits
        byte[] keyBytes = Encoding.UTF8.GetBytes(key);
        var keyBits = BitString.FromBytes(keyBytes);

        return TryReadInternal(keyBits, 0, 0, out value);
    }

    private bool TryReadInternal(BitString keyBits, int keyBitIndex, int nodeBitPos, out long value)
    {
        value = 0;
        var reader = new BitArrayReader(_buffer, nodeBitPos);

        var (hasValue, hasChildren, isDeadEnd) = ReadNodeHeader(ref reader);

        if (isDeadEnd)
            return false;

        // Read node header
        _ = VarInt.Read(ref reader);
        int prefixLength = VarInt.Read(ref reader);

        // Match prefix bits against key bits
        for (int i = 0; i < prefixLength; i++)
        {
            if (keyBitIndex >= keyBits.Length)
            {
                // Key exhausted before prefix - no match
                return false;
            }

            bool prefixBit = reader.ReadBit();
            bool keyBit = keyBits[keyBitIndex];

            if (prefixBit != keyBit)
            {
                // Mismatch
                return false;
            }
            keyBitIndex++;
        }

        // All key bits matched?
        if (keyBitIndex == keyBits.Length)
        {
            if (hasValue)
            {
                // Read value
                uint low = reader.ReadBits(32);
                uint high = reader.ReadBits(32);
                value = (long)low | ((long)high << 32);
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

        // Read left child info to determine positions
        int leftChildPos = reader.BitPosition;
        var (_, _, leftIsDeadEnd) = ReadNodeHeader(ref reader);

        int rightChildPos;
        if (leftIsDeadEnd)
        {
            rightChildPos = leftChildPos + FlatTrieNode.DeadEndSize;
        }
        else
        {
            int leftSize = ReadNodeSize(leftChildPos);
            rightChildPos = leftChildPos + leftSize;
        }

        if (nextBit == false)
        {
            // Go left
            if (leftIsDeadEnd)
                return false;
            return TryReadInternal(keyBits, keyBitIndex, leftChildPos, out value);
        }
        else
        {
            // Go right
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

    public bool TryWrite(string key, long value)
    {
        if (string.IsNullOrEmpty(key))
            return false;

        RebuildIfStale();

        // Convert key to bits
        byte[] keyBytes = Encoding.UTF8.GetBytes(key);
        var keyBits = BitString.FromBytes(keyBytes);

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
        int nodeSize = FlatTrieNode.CalculateNodeSize(true, false, prefixLength);

        if (nodeSize > BufferSizeBits)
            return false;

        var writer = new BitArrayWriter(_buffer);

        // Write the node
        writer.WriteBit(true);  // HasValue
        writer.WriteBit(false); // HasChildren
        VarInt.Write(ref writer, nodeSize);
        VarInt.Write(ref writer, prefixLength);
        writer.WriteBitString(keyBits);
        writer.WriteBits((uint)(value & 0xFFFFFFFF), 32);
        writer.WriteBits((uint)(value >> 32), 32);

        _usedBits = nodeSize;
        return true;
    }

    private bool TryWriteInternal(BitString keyBits, int keyBitIndex, int nodeBitPos, long value, List<int> ancestors)
    {
        var reader = new BitArrayReader(_buffer, nodeBitPos);

        var (hasValue, hasChildren, isDeadEnd) = ReadNodeHeader(ref reader);

        // Dead end - this shouldn't happen if called correctly
        if (isDeadEnd)
            return false;
        _ = VarInt.Read(ref reader);
        int prefixLength = VarInt.Read(ref reader);
        int prefixStartPos = reader.BitPosition;

        // Match prefix bits
        int matchedBits = 0;
        for (int i = 0; i < prefixLength && keyBitIndex + i < keyBits.Length; i++)
        {
            bool prefixBit = reader.ReadBit();
            bool keyBit = keyBits[keyBitIndex + i];

            if (prefixBit != keyBit)
                break;

            matchedBits++;
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

        int leftChildPos = reader.BitPosition;
        var (_, _, leftIsDeadEnd) = ReadNodeHeader(ref reader);

        int rightChildPos;
        if (leftIsDeadEnd)
        {
            rightChildPos = leftChildPos + FlatTrieNode.DeadEndSize;
        }
        else
        {
            int leftSize = ReadNodeSize(leftChildPos);
            rightChildPos = leftChildPos + leftSize;
        }

        if (nextBit == false)
        {
            // Go left
            if (leftIsDeadEnd)
            {
                return ReplaceDeadEnd(leftChildPos, keyBits, keyBitIndex, value, ancestors);
            }
            return TryWriteInternal(keyBits, keyBitIndex, leftChildPos, value, ancestors);
        }
        else
        {
            // Go right
            reader.Seek(rightChildPos);
            (_, _, bool rightIsDeadEnd) = ReadNodeHeader(ref reader);

            if (rightIsDeadEnd)
            {
                return ReplaceDeadEnd(rightChildPos, keyBits, keyBitIndex, value, ancestors);
            }
            return TryWriteInternal(keyBits, keyBitIndex, rightChildPos, value, ancestors);
        }
    }

    private bool UpdateNodeValue(int nodeBitPos, long value)
    {


        // Just update existing value in place
        var reader = new BitArrayReader(_buffer, nodeBitPos);
        reader.ReadBit(); // HasValue
        reader.ReadBit(); // HasChildren
        VarInt.Read(ref reader); // Size
        int prefixLength = VarInt.Read(ref reader);
        reader.Skip(prefixLength); // Skip prefix

        // Now at value position
        var writer = new BitArrayWriter(_buffer, reader.BitPosition);
        writer.WriteBits((uint)(value & 0xFFFFFFFF), 32);
        writer.WriteBits((uint)(value >> 32), 32);

        return true;
    }

    private bool RewriteNodeWithValue(int nodeBitPos, long value, List<int> ancestors)
    {
        // Read current node
        var reader = new BitArrayReader(_buffer, nodeBitPos);

        var (hasValue, hasChildren, isDeadEnd) = ReadNodeHeader(ref reader);
        int oldSize = VarInt.Read(ref reader);
        int prefixLength = VarInt.Read(ref reader);

        // Read prefix
        var prefixBits = new uint[(prefixLength + 31) / 32];
        var prefixWriter = new BitArrayWriter(prefixBits);
        for (int i = 0; i < prefixLength; i++)
        {
            prefixWriter.WriteBit(reader.ReadBit());
        }

        // Calculate new size (adding 64 bits for value)
        int childrenSize = hasChildren ? (oldSize - (reader.BitPosition - nodeBitPos)) : 0;
        int newSize = FlatTrieNode.CalculateNodeSize(true, hasChildren, prefixLength, childrenSize);

        int delta = newSize - oldSize;

        // Shift everything after this node
        if (!ShiftBits(nodeBitPos + oldSize, delta))
            return false;

        // Rewrite node with value
        var writer = new BitArrayWriter(_buffer, nodeBitPos);
        writer.WriteBit(true);  // HasValue
        writer.WriteBit(hasChildren);
        VarInt.Write(ref writer, newSize);
        VarInt.Write(ref writer, prefixLength);

        // Write prefix
        var prefixReader = new BitArrayReader(prefixBits);
        for (int i = 0; i < prefixLength; i++)
        {
            writer.WriteBit(prefixReader.ReadBit());
        }

        // Write value
        writer.WriteBits((uint)(value & 0xFFFFFFFF), 32);
        writer.WriteBits((uint)(value >> 32), 32);

        // Children were already shifted, and now follow naturally

        _usedBits += delta;
        UpdateAncestorSizes(ancestors, delta);

        return true;
    }

    private bool SplitNode(int nodeBitPos, BitString keyBits, int keyBitIndex, int matchedBits, long newValue, List<int> ancestors)
    {
        // Read current node completely first
        var reader = new BitArrayReader(_buffer, nodeBitPos);
        var (oldHasValue, oldHasChildren, _) = ReadNodeHeader(ref reader);
        int oldSize = VarInt.Read(ref reader);
        int oldPrefixLength = VarInt.Read(ref reader);

        // Read the entire old prefix
        var oldPrefixBits = new uint[(oldPrefixLength + 31) / 32];
        var prefixWriter = new BitArrayWriter(oldPrefixBits);
        for (int i = 0; i < oldPrefixLength; i++)
        {
            prefixWriter.WriteBit(reader.ReadBit());
        }

        // Read old value if present
        long oldValue = 0;
        if (oldHasValue)
        {
            uint low = reader.ReadBits(32);
            uint high = reader.ReadBits(32);
            oldValue = (long)low | ((long)high << 32);
        }

        // Save children data if present (BEFORE any shifting)
        int childrenStartPos = reader.BitPosition;
        int childrenSize = oldHasChildren ? (oldSize - (childrenStartPos - nodeBitPos)) : 0;
        uint[]? childrenCopy = null;
        if (oldHasChildren && childrenSize > 0)
        {
            childrenCopy = new uint[(childrenSize + 31) / 32];
            var childWriter = new BitArrayWriter(childrenCopy);
            for (int i = 0; i < childrenSize; i++)
            {
                childWriter.WriteBit(reader.ReadBit());
            }
        }

        // The diverging bits
        var oldPrefixReader = new BitArrayReader(oldPrefixBits, matchedBits);
        bool oldDivergeBit = oldPrefixReader.ReadBit();

        // Old node's remaining prefix (after the diverge bit)
        int oldRemainingPrefixLen = oldPrefixLength - matchedBits - 1;

        // New key's remaining bits (after the diverge bit)
        int newRemainingKeyLen = keyBits.Length - keyBitIndex - matchedBits - 1;

        // Calculate sizes
        int oldChildSize = FlatTrieNode.CalculateNodeSize(oldHasValue, oldHasChildren, oldRemainingPrefixLen, childrenSize);
        int newChildSize = FlatTrieNode.CalculateNodeSize(true, false, newRemainingKeyLen);

        int newParentSize = FlatTrieNode.CalculateNodeSize(false, true, matchedBits, oldChildSize + newChildSize);
        int delta = newParentSize - oldSize;

        // Check space
        if (_usedBits + delta > BufferSizeBits)
            return false;

        // Shift bits after old node
        if (!ShiftBits(nodeBitPos + oldSize, delta))
            return false;

        // Write new parent node
        var writer = new BitArrayWriter(_buffer, nodeBitPos);
        writer.WriteBit(false); // HasValue
        writer.WriteBit(true);  // HasChildren
        VarInt.Write(ref writer, newParentSize);
        VarInt.Write(ref writer, matchedBits);

        // Write matched prefix
        var matchedPrefixReader = new BitArrayReader(oldPrefixBits);
        for (int i = 0; i < matchedBits; i++)
        {
            writer.WriteBit(matchedPrefixReader.ReadBit());
        }

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

        _usedBits += delta;
        UpdateAncestorSizes(ancestors, delta);

        return true;
    }

    private void WriteOldContentAsChild(ref BitArrayWriter writer, bool hasValue, bool hasChildren,
        uint[] prefixBits, int prefixStartBit, int prefixLength, long value,
        uint[]? childrenData, int childrenSize, int totalSize)
    {
        writer.WriteBit(hasValue);
        writer.WriteBit(hasChildren);
        VarInt.Write(ref writer, totalSize);
        VarInt.Write(ref writer, prefixLength);

        // Write remaining prefix
        var prefixReader = new BitArrayReader(prefixBits, prefixStartBit);
        for (int i = 0; i < prefixLength; i++)
        {
            writer.WriteBit(prefixReader.ReadBit());
        }

        if (hasValue)
        {
            writer.WriteBits((uint)(value & 0xFFFFFFFF), 32);
            writer.WriteBits((uint)(value >> 32), 32);
        }

        // Copy children data if present
        if (hasChildren && childrenData != null && childrenSize > 0)
        {
            var childReader = new BitArrayReader(childrenData);
            for (int i = 0; i < childrenSize; i++)
            {
                writer.WriteBit(childReader.ReadBit());
            }
        }
    }

    private void WriteNewKeyAsChild(ref BitArrayWriter writer, BitString keyBits, int startIndex, int prefixLength, long value, int totalSize)
    {
        writer.WriteBit(true);  // HasValue
        writer.WriteBit(false); // HasChildren
        VarInt.Write(ref writer, totalSize);
        VarInt.Write(ref writer, prefixLength);

        // Write remaining key bits as prefix
        for (int i = 0; i < prefixLength; i++)
        {
            writer.WriteBit(keyBits[startIndex + i]);
        }

        // Write value
        writer.WriteBits((uint)(value & 0xFFFFFFFF), 32);
        writer.WriteBits((uint)(value >> 32), 32);
    }

    private bool SplitNodeKeyExhausted(int nodeBitPos, BitString keyBits, int keyBitIndex, int matchedBits, long newValue, List<int> ancestors)
    {
        // The new key ends within the existing prefix.
        // New structure:
        // - This node: prefix = first matchedBits, HasValue = true (new key's value), HasChildren = true
        // - One child contains old content (remaining prefix + old value + old children)
        // - Other child is dead end

        // Read current node
        var reader = new BitArrayReader(_buffer, nodeBitPos);
        bool oldHasValue = reader.ReadBit();
        bool oldHasChildren = reader.ReadBit();
        int oldSize = VarInt.Read(ref reader);
        int oldPrefixLength = VarInt.Read(ref reader);

        // Read the entire old prefix
        var oldPrefixBits = new uint[(oldPrefixLength + 31) / 32];
        var prefixWriter = new BitArrayWriter(oldPrefixBits);
        for (int i = 0; i < oldPrefixLength; i++)
        {
            prefixWriter.WriteBit(reader.ReadBit());
        }

        // Read old value if present
        long oldValue = 0;
        if (oldHasValue)
        {
            uint low = reader.ReadBits(32);
            uint high = reader.ReadBits(32);
            oldValue = (long)low | ((long)high << 32);
        }

        // Save children data if present
        int childrenStartPos = reader.BitPosition;
        int childrenSize = oldHasChildren ? (oldSize - (childrenStartPos - nodeBitPos)) : 0;
        uint[]? childrenCopy = null;
        if (oldHasChildren && childrenSize > 0)
        {
            childrenCopy = new uint[(childrenSize + 31) / 32];
            var childWriter = new BitArrayWriter(childrenCopy);
            for (int i = 0; i < childrenSize; i++)
            {
                childWriter.WriteBit(reader.ReadBit());
            }
        }

        // The bit after the matched portion determines which child branch
        var oldPrefixReader = new BitArrayReader(oldPrefixBits, matchedBits);
        bool oldNextBit = oldPrefixReader.ReadBit();

        // Old node's remaining prefix (after the branch bit)
        int oldRemainingPrefixLen = oldPrefixLength - matchedBits - 1;

        // Calculate sizes
        int oldChildSize = FlatTrieNode.CalculateNodeSize(oldHasValue, oldHasChildren, oldRemainingPrefixLen, childrenSize);

        // New parent: HasValue=true, HasChildren=true, one child is old content, other is dead end
        int newParentChildrenSize = oldChildSize + FlatTrieNode.DeadEndSize;
        int newParentSize = FlatTrieNode.CalculateNodeSize(true, true, matchedBits, newParentChildrenSize);
        int delta = newParentSize - oldSize;

        // Check space
        if (_usedBits + delta > BufferSizeBits)
            return false;

        // Shift bits after old node
        if (!ShiftBits(nodeBitPos + oldSize, delta))
            return false;

        // Write new parent node
        var writer = new BitArrayWriter(_buffer, nodeBitPos);
        writer.WriteBit(true);  // HasValue (new key's value)
        writer.WriteBit(true);  // HasChildren
        VarInt.Write(ref writer, newParentSize);
        VarInt.Write(ref writer, matchedBits);

        // Write matched prefix (the new key's bits)
        var matchedPrefixReader = new BitArrayReader(oldPrefixBits);
        for (int i = 0; i < matchedBits; i++)
        {
            writer.WriteBit(matchedPrefixReader.ReadBit());
        }

        // Write new value
        writer.WriteBits((uint)(newValue & 0xFFFFFFFF), 32);
        writer.WriteBits((uint)(newValue >> 32), 32);

        // Write children: old content on one side, dead end on other
        if (oldNextBit == false)
        {
            // Old content goes left
            WriteOldContentAsChild(ref writer, oldHasValue, oldHasChildren, oldPrefixBits, matchedBits + 1,
                oldRemainingPrefixLen, oldValue, childrenCopy, childrenSize, oldChildSize);
            FlatTrieNode.WriteDeadEnd(ref writer);
        }
        else
        {
            // Old content goes right
            FlatTrieNode.WriteDeadEnd(ref writer);
            WriteOldContentAsChild(ref writer, oldHasValue, oldHasChildren, oldPrefixBits, matchedBits + 1,
                oldRemainingPrefixLen, oldValue, childrenCopy, childrenSize, oldChildSize);
        }

        _usedBits += delta;
        UpdateAncestorSizes(ancestors, delta);

        return true;
    }

    private bool AddChildToLeaf(int nodeBitPos, BitString keyBits, int keyBitIndex, long value, List<int> ancestors)
    {
        // Read current leaf node
        var reader = new BitArrayReader(_buffer, nodeBitPos);
        bool hasValue = reader.ReadBit();
        reader.ReadBit(); // HasChildren (false)
        int oldSize = VarInt.Read(ref reader);
        int prefixLength = VarInt.Read(ref reader);

        // Read prefix
        var prefixBits = new uint[(prefixLength + 31) / 32];
        var prefixWriter = new BitArrayWriter(prefixBits);
        for (int i = 0; i < prefixLength; i++)
        {
            prefixWriter.WriteBit(reader.ReadBit());
        }

        // Read value
        long existingValue = 0;
        if (hasValue)
        {
            uint low = reader.ReadBits(32);
            uint high = reader.ReadBits(32);
            existingValue = (long)low | ((long)high << 32);
        }

        // The next bit determines which child
        bool nextBit = keyBits[keyBitIndex];
        keyBitIndex++;

        int remainingKeyLen = keyBits.Length - keyBitIndex;
        int newChildSize = FlatTrieNode.CalculateNodeSize(true, false, remainingKeyLen);

        // New structure: this node gets HasChildren=true, with one real child and one dead end
        int childrenSize = newChildSize + FlatTrieNode.DeadEndSize;
        int newSize = FlatTrieNode.CalculateNodeSize(hasValue, true, prefixLength, childrenSize);
        int delta = newSize - oldSize;

        if (_usedBits + delta > BufferSizeBits)
            return false;

        if (!ShiftBits(nodeBitPos + oldSize, delta))
            return false;

        // Rewrite node
        var writer = new BitArrayWriter(_buffer, nodeBitPos);
        writer.WriteBit(hasValue);
        writer.WriteBit(true); // HasChildren now
        VarInt.Write(ref writer, newSize);
        VarInt.Write(ref writer, prefixLength);

        var prefixReader = new BitArrayReader(prefixBits);
        for (int i = 0; i < prefixLength; i++)
        {
            writer.WriteBit(prefixReader.ReadBit());
        }

        if (hasValue)
        {
            writer.WriteBits((uint)(existingValue & 0xFFFFFFFF), 32);
            writer.WriteBits((uint)(existingValue >> 32), 32);
        }

        // Write children
        if (nextBit == false)
        {
            // New child goes left
            WriteNewKeyAsChild(ref writer, keyBits, keyBitIndex, remainingKeyLen, value, newChildSize);
            FlatTrieNode.WriteDeadEnd(ref writer);
        }
        else
        {
            // New child goes right
            FlatTrieNode.WriteDeadEnd(ref writer);
            WriteNewKeyAsChild(ref writer, keyBits, keyBitIndex, remainingKeyLen, value, newChildSize);
        }

        _usedBits += delta;
        UpdateAncestorSizes(ancestors, delta);

        return true;
    }

    private bool ReplaceDeadEnd(int deadEndPos, BitString keyBits, int keyBitIndex, long value, List<int> ancestors)
    {
        int remainingKeyLen = keyBits.Length - keyBitIndex;
        int newNodeSize = FlatTrieNode.CalculateNodeSize(true, false, remainingKeyLen);
        int delta = newNodeSize - FlatTrieNode.DeadEndSize;

        if (_usedBits + delta > BufferSizeBits)
            return false;

        if (!ShiftBits(deadEndPos + FlatTrieNode.DeadEndSize, delta))
            return false;

        var writer = new BitArrayWriter(_buffer, deadEndPos);
        WriteNewKeyAsChild(ref writer, keyBits, keyBitIndex, remainingKeyLen, value, newNodeSize);

        _usedBits += delta;
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
            if (_usedBits + delta > BufferSizeBits)
                return false;

            // Shift from end to avoid overwriting
            for (int i = _usedBits - 1; i >= fromBitPos; i--)
            {
                int srcUint = i / 32;
                int srcBit = i % 32;
                int dstUint = (i + delta) / 32;
                int dstBit = (i + delta) % 32;

                bool bit = (_buffer[srcUint] & (1u << srcBit)) != 0;
                if (bit)
                    _buffer[dstUint] |= (1u << dstBit);
                else
                    _buffer[dstUint] &= ~(1u << dstBit);
            }
        }
        else
        {
            // Shrinking - shift left
            delta = -delta;
            for (int i = fromBitPos; i < _usedBits; i++)
            {
                int srcUint = i / 32;
                int srcBit = i % 32;
                int dstUint = (i - delta) / 32;
                int dstBit = (i - delta) % 32;

                bool bit = (_buffer[srcUint] & (1u << srcBit)) != 0;
                if (bit)
                    _buffer[dstUint] |= (1u << dstBit);
                else
                    _buffer[dstUint] &= ~(1u << dstBit);
            }
        }

        return true;
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
            int currentSize = VarInt.Read(ref reader);
            int currentClass = VarInt.GetClass(currentSize);

            int newSize = currentSize + delta;
            int newClass = VarInt.GetClass(newSize);

            if (newClass <= currentClass)
            {
                // Fits in current encoding - update in place using same class
                var writer = new BitArrayWriter(_buffer, sizePos);
                VarInt.WriteWithClass(ref writer, newSize, currentClass);
            }
            else
            {
                // Needs larger encoding - mark trie as needing rebuild
                _sizesStale = true;
                return; // Stop updating, will rebuild
            }
        }
    }

    /// <summary>
    /// Rebuild the trie to fix stale sizes.
    /// Collects all key-value pairs and reinserts them.
    /// </summary>
    private void RebuildIfStale()
    {
        if (!_sizesStale)
            return;

        // Collect all key-value pairs
        var entries = new List<(string key, long value)>();
        CollectEntries(0, new List<bool>(), entries);

        // Clear buffer
        Array.Clear(_buffer);
        _usedBits = 0;
        _sizesStale = false;

        // Reinsert all entries
        foreach (var (key, value) in entries)
        {
            TryWrite(key, value);
        }
    }

    /// <summary>
    /// Collect all key-value pairs from the trie.
    /// </summary>
    private void CollectEntries(int nodeBitPos, List<bool> keyBits, List<(string key, long value)> entries)
    {
        var reader = new BitArrayReader(_buffer, nodeBitPos);
        bool hasValue = reader.ReadBit();
        bool hasChildren = reader.ReadBit();

        if (!hasValue && !hasChildren)
            return; // Dead end

        VarInt.Read(ref reader); // Size (may be stale, but we don't need it for collection)
        int prefixLength = VarInt.Read(ref reader);

        // Read and append prefix bits
        for (int i = 0; i < prefixLength; i++)
        {
            keyBits.Add(reader.ReadBit());
        }

        if (hasValue)
        {
            uint low = reader.ReadBits(32);
            uint high = reader.ReadBits(32);
            long value = (long)low | ((long)high << 32);

            // Convert bits to string
            string key = BitsToString(keyBits);
            if (!string.IsNullOrEmpty(key))
            {
                entries.Add((key, value));
            }
        }

        if (hasChildren)
        {
            // Left child (bit 0)
            int leftChildPos = reader.BitPosition;
            keyBits.Add(false);
            CollectEntries(leftChildPos, keyBits, entries);
            keyBits.RemoveAt(keyBits.Count - 1);

            // Find right child position by calculating left size
            int leftSize = CalculateNodeSizeSimple(leftChildPos);
            int rightChildPos = leftChildPos + leftSize;

            // Right child (bit 1)
            keyBits.Add(true);
            CollectEntries(rightChildPos, keyBits, entries);
            keyBits.RemoveAt(keyBits.Count - 1);
        }

        // Remove prefix bits we added
        keyBits.RemoveRange(keyBits.Count - prefixLength, prefixLength);
    }

    /// <summary>
    /// Simple size calculation that doesn't rely on stored sizes.
    /// </summary>
    private int CalculateNodeSizeSimple(int nodeBitPos)
    {
        var reader = new BitArrayReader(_buffer, nodeBitPos);
        bool hasValue = reader.ReadBit();
        bool hasChildren = reader.ReadBit();

        if (!hasValue && !hasChildren)
            return FlatTrieNode.DeadEndSize;

        int sizeStart = reader.BitPosition;
        VarInt.Read(ref reader); // Skip size
        int sizeFieldBits = reader.BitPosition - sizeStart;

        int prefixLenStart = reader.BitPosition;
        int prefixLength = VarInt.Read(ref reader);
        int prefixLenBits = reader.BitPosition - prefixLenStart;

        reader.Skip(prefixLength);
        if (hasValue) reader.Skip(64);

        int childrenSize = 0;
        if (hasChildren)
        {
            int leftChildPos = reader.BitPosition;
            int leftSize = CalculateNodeSizeSimple(leftChildPos);
            int rightChildPos = leftChildPos + leftSize;
            int rightSize = CalculateNodeSizeSimple(rightChildPos);
            childrenSize = leftSize + rightSize;
        }

        return 2 + sizeFieldBits + prefixLenBits + prefixLength + (hasValue ? 64 : 0) + childrenSize;
    }

    /// <summary>
    /// Convert a list of bits back to a UTF-8 string.
    /// </summary>
    private static string BitsToString(List<bool> bits)
    {
        if (bits.Count == 0 || bits.Count % 8 != 0)
            return string.Empty;

        byte[] bytes = new byte[bits.Count / 8];
        for (int i = 0; i < bytes.Length; i++)
        {
            byte b = 0;
            for (int j = 0; j < 8; j++)
            {
                if (bits[i * 8 + j])
                    b |= (byte)(1 << j);
            }
            bytes[i] = b;
        }

        try
        {
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Read a node's size. After RebuildIfStale(), sizes are always valid.
    /// </summary>
    private int ReadNodeSize(int nodeBitPos)
    {
        var reader = new BitArrayReader(_buffer, nodeBitPos);

        var (_, _, isDeadEnd) = ReadNodeHeader(ref reader);

        if (isDeadEnd)
            return FlatTrieNode.DeadEndSize; // Dead end

        return VarInt.Read(ref reader);
    }

    public void Delete(string key)
    {
        if (string.IsNullOrEmpty(key))
            return;

        byte[] keyBytes = Encoding.UTF8.GetBytes(key);
        var keyBits = BitString.FromBytes(keyBytes);

        // Find the node and clear HasValue
        DeleteInternal(keyBits, 0, 0);
    }

    private bool DeleteInternal(BitString keyBits, int keyBitIndex, int nodeBitPos)
    {
        var reader = new BitArrayReader(_buffer, nodeBitPos);

        var (hasValue, hasChildren, isDeadEnd) = ReadNodeHeader(ref reader);
        if (isDeadEnd)
            return false; // Dead end
        _ = VarInt.Read(ref reader);
        int prefixLength = VarInt.Read(ref reader);

        // Match prefix
        for (int i = 0; i < prefixLength; i++)
        {
            if (keyBitIndex >= keyBits.Length)
                return false;

            bool prefixBit = reader.ReadBit();
            if (prefixBit != keyBits[keyBitIndex])
                return false;

            keyBitIndex++;
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

        int leftChildPos = reader.BitPosition;

        var (_, _, leftIsDeadEnd) = ReadNodeHeader(ref reader);

        int rightChildPos;
        if (leftIsDeadEnd)
        {
            rightChildPos = leftChildPos + FlatTrieNode.DeadEndSize;
        }
        else
        {
            int leftSize = ReadNodeSize(leftChildPos); // Handles stale sizes
            rightChildPos = leftChildPos + leftSize;
        }

        if (nextBit == false)
        {
            return DeleteInternal(keyBits, keyBitIndex, leftChildPos);
        }
        else
        {
            return DeleteInternal(keyBits, keyBitIndex, rightChildPos);
        }
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

        // Recalculate used bits by finding the root size
        var reader = new BitArrayReader(_buffer);
        bool hasValue = reader.ReadBit();
        bool hasChildren = reader.ReadBit();

        if (!hasValue && !hasChildren)
        {
            _usedBits = 0;
        }
        else
        {
            _usedBits = VarInt.Read(ref reader);
        }
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
        _ = VarInt.Read(ref reader);
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

            int leftChildPos = reader.BitPosition;
            (_, _, var leftIsDeadEnd) = ReadNodeHeader(ref reader);

            int rightChildPos;
            if (leftIsDeadEnd)
            {
                rightChildPos = leftChildPos + FlatTrieNode.DeadEndSize;
            }
            else
            {
                int leftSize = ReadNodeSize(leftChildPos); // Handles stale sizes
                rightChildPos = leftChildPos + leftSize;
            }

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
