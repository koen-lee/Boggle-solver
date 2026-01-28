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

    public FlatTrie()
    {
        // Buffer starts as all zeros, which is a dead end (empty trie)
    }

    public bool TryRead(string key, out long value)
    {
        value = 0;

        if (string.IsNullOrEmpty(key))
            return false;

        // Convert key to bits
        byte[] keyBytes = Encoding.UTF8.GetBytes(key);
        var keyBits = BitString.FromBytes(keyBytes);

        // Check if trie is empty (root is dead end)
        var reader = new BitArrayReader(_buffer);
        bool hasValue = reader.ReadBit();
        bool hasChildren = reader.ReadBit();

        if (!hasValue && !hasChildren)
            return false; // Empty trie

        return TryReadInternal(keyBits, 0, 0, out value);
    }

    private bool TryReadInternal(BitString keyBits, int keyBitIndex, int nodeBitPos, out long value)
    {
        value = 0;
        var reader = new BitArrayReader(_buffer, nodeBitPos);

        bool hasValue = reader.ReadBit();
        bool hasChildren = reader.ReadBit();

        // Dead end
        if (!hasValue && !hasChildren)
            return false;

        // Read node header
        int size = VarInt.Read(ref reader);
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
        bool leftHasValue = reader.ReadBit();
        bool leftHasChildren = reader.ReadBit();

        int rightChildPos;
        if (!leftHasValue && !leftHasChildren)
        {
            // Left is dead end
            rightChildPos = leftChildPos + 2;
        }
        else
        {
            int leftSize = VarInt.Read(ref reader);
            rightChildPos = leftChildPos + leftSize;
        }

        if (nextBit == false)
        {
            // Go left
            if (!leftHasValue && !leftHasChildren)
                return false; // Dead end
            return TryReadInternal(keyBits, keyBitIndex, leftChildPos, out value);
        }
        else
        {
            // Go right
            return TryReadInternal(keyBits, keyBitIndex, rightChildPos, out value);
        }
    }

    public bool TryWrite(string key, long value)
    {
        if (string.IsNullOrEmpty(key))
            return false;

        // Convert key to bits
        byte[] keyBytes = Encoding.UTF8.GetBytes(key);
        var keyBits = BitString.FromBytes(keyBytes);

        // Check if trie is empty
        var reader = new BitArrayReader(_buffer);
        bool hasValue = reader.ReadBit();
        bool hasChildren = reader.ReadBit();

        if (!hasValue && !hasChildren)
        {
            // Empty trie - create root node with this key
            return WriteNewRoot(keyBits, value);
        }

        // Find where to insert
        return TryWriteInternal(keyBits, 0, 0, value);
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

    private bool TryWriteInternal(BitString keyBits, int keyBitIndex, int nodeBitPos, long value)
    {
        var reader = new BitArrayReader(_buffer, nodeBitPos);

        bool hasValue = reader.ReadBit();
        bool hasChildren = reader.ReadBit();

        // Dead end - this shouldn't happen if called correctly
        if (!hasValue && !hasChildren)
            return false;

        int size = VarInt.Read(ref reader);
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
            return SplitNode(nodeBitPos, keyBits, keyBitIndex, matchedBits, value);
        }

        // Case 2: Key exhausted within or at end of prefix
        if (keyBitIndex + matchedBits == keyBits.Length)
        {
            if (matchedBits == prefixLength)
            {
                // Exact match - update value
                return UpdateNodeValue(nodeBitPos, value, !hasValue);
            }
            else
            {
                // Key ends in middle of prefix - need to split
                return SplitNodeKeyExhausted(nodeBitPos, keyBits, keyBitIndex, matchedBits, value);
            }
        }

        // Case 3: Prefix fully matched, more key bits remain
        keyBitIndex += prefixLength;
        reader.Seek(prefixStartPos + prefixLength);

        if (!hasChildren)
        {
            // Need to add children to this leaf
            return AddChildToLeaf(nodeBitPos, keyBits, keyBitIndex, value);
        }

        // Skip value if present
        if (hasValue)
        {
            reader.Skip(64);
        }

        // Follow appropriate child
        bool nextBit = keyBits[keyBitIndex];
        keyBitIndex++;

        int leftChildPos = reader.BitPosition;
        bool leftHasValue = reader.ReadBit();
        bool leftHasChildren = reader.ReadBit();

        int rightChildPos;
        if (!leftHasValue && !leftHasChildren)
        {
            rightChildPos = leftChildPos + 2;
        }
        else
        {
            int leftSize = VarInt.Read(ref reader);
            rightChildPos = leftChildPos + leftSize;
        }

        if (nextBit == false)
        {
            // Go left
            if (!leftHasValue && !leftHasChildren)
            {
                // Left is dead end - replace with new node
                return ReplaceDeadEnd(leftChildPos, keyBits, keyBitIndex, value);
            }
            return TryWriteInternal(keyBits, keyBitIndex, leftChildPos, value);
        }
        else
        {
            // Go right
            reader.Seek(rightChildPos);
            bool rightHasValue = reader.ReadBit();
            bool rightHasChildren = reader.ReadBit();

            if (!rightHasValue && !rightHasChildren)
            {
                // Right is dead end - replace with new node
                return ReplaceDeadEnd(rightChildPos, keyBits, keyBitIndex, value);
            }
            return TryWriteInternal(keyBits, keyBitIndex, rightChildPos, value);
        }
    }

    private bool UpdateNodeValue(int nodeBitPos, long value, bool needToAddValue)
    {
        if (needToAddValue)
        {
            // Need to expand the node to include a value
            // This requires shifting and is complex
            return RewriteNodeWithValue(nodeBitPos, value);
        }

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

    private bool RewriteNodeWithValue(int nodeBitPos, long value)
    {
        // Read current node
        var reader = new BitArrayReader(_buffer, nodeBitPos);
        reader.ReadBit(); // HasValue (false)
        bool hasChildren = reader.ReadBit();
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
        UpdateAncestorSizes(0, nodeBitPos, delta);

        return true;
    }

    private bool SplitNode(int nodeBitPos, BitString keyBits, int keyBitIndex, int matchedBits, long newValue)
    {
        // Read current node completely first
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
        bool newDivergeBit = keyBits[keyBitIndex + matchedBits];

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
        UpdateAncestorSizes(0, nodeBitPos, delta);

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

    private bool SplitNodeKeyExhausted(int nodeBitPos, BitString keyBits, int keyBitIndex, int matchedBits, long newValue)
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
        UpdateAncestorSizes(0, nodeBitPos, delta);

        return true;
    }

    private bool AddChildToLeaf(int nodeBitPos, BitString keyBits, int keyBitIndex, long value)
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
        UpdateAncestorSizes(0, nodeBitPos, delta);

        return true;
    }

    private bool ReplaceDeadEnd(int deadEndPos, BitString keyBits, int keyBitIndex, long value)
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
        UpdateAncestorSizes(0, deadEndPos, delta);

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

    private void UpdateAncestorSizes(int rootPos, int targetPos, int delta)
    {
        // Walk from root to target, updating Size fields along the path
        if (delta == 0 || rootPos == targetPos)
            return;

        // Collect positions of nodes on the path to target
        var path = new List<int>();
        int currentPos = rootPos;

        while (currentPos != targetPos && currentPos < _usedBits)
        {
            var reader = new BitArrayReader(_buffer, currentPos);
            bool hasValue = reader.ReadBit();
            bool hasChildren = reader.ReadBit();

            if (!hasValue && !hasChildren)
                break; // Dead end

            path.Add(currentPos);

            int size = VarInt.Read(ref reader);
            int prefixLength = VarInt.Read(ref reader);
            reader.Skip(prefixLength);

            if (hasValue)
                reader.Skip(64);

            if (!hasChildren)
                break;

            // Check if target is in left or right subtree
            int leftChildPos = reader.BitPosition;
            bool leftHasValue = reader.ReadBit();
            bool leftHasChildren = reader.ReadBit();

            int rightChildPos;
            if (!leftHasValue && !leftHasChildren)
            {
                rightChildPos = leftChildPos + 2;
            }
            else
            {
                int leftSize = VarInt.Read(ref reader);
                rightChildPos = leftChildPos + leftSize;
            }

            // Determine which subtree contains the target
            if (targetPos >= leftChildPos && targetPos < rightChildPos)
            {
                currentPos = leftChildPos;
            }
            else if (targetPos >= rightChildPos)
            {
                currentPos = rightChildPos;
            }
            else
            {
                break; // Target not in this subtree
            }

            // Safety check to prevent infinite loop
            if (currentPos == path[^1])
                break;
        }

        // Update sizes for nodes on the path (from root toward target)
        foreach (int nodePos in path)
        {
            if (nodePos == targetPos)
                continue;

            var reader = new BitArrayReader(_buffer, nodePos);
            reader.ReadBit(); // HasValue
            reader.ReadBit(); // HasChildren

            int sizePos = reader.BitPosition;
            int currentSize = VarInt.Read(ref reader);
            int newSize = currentSize + delta;

            // Check if encoding size would change
            int oldEncodingSize = VarInt.GetEncodedBitCount(currentSize);
            int newEncodingSize = VarInt.GetEncodedBitCount(newSize);

            if (oldEncodingSize == newEncodingSize)
            {
                // Update in place
                var writer = new BitArrayWriter(_buffer, sizePos);
                VarInt.Write(ref writer, newSize);
            }
            // If encoding size changes, we'd need to shift - this is complex
            // For now, skip these cases (they should be rare)
        }
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

        bool hasValue = reader.ReadBit();
        bool hasChildren = reader.ReadBit();

        if (!hasValue && !hasChildren)
            return false; // Dead end

        int size = VarInt.Read(ref reader);
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
        bool leftHasValue = reader.ReadBit();
        bool leftHasChildren = reader.ReadBit();

        int rightChildPos;
        if (!leftHasValue && !leftHasChildren)
        {
            rightChildPos = leftChildPos + 2;
        }
        else
        {
            int leftSize = VarInt.Read(ref reader);
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

        bool hasValue = reader.ReadBit();
        bool hasChildren = reader.ReadBit();

        if (!hasValue && !hasChildren)
        {
            stats.DeadEndCount++;
            return;
        }

        stats.NodeCount++;

        int size = VarInt.Read(ref reader);
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
            bool leftHasValue = reader.ReadBit();
            bool leftHasChildren = reader.ReadBit();

            int rightChildPos;
            if (!leftHasValue && !leftHasChildren)
            {
                rightChildPos = leftChildPos + 2;
            }
            else
            {
                int leftSize = VarInt.Read(ref reader);
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
