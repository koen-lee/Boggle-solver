using BitUtilities;

namespace FlatTrie;

/// <summary>
/// Represents a node in the flat trie.
///
/// Node layout (in bits):
///   Flags (2 bits):
///     HasValue (1 bit)
///     HasChildren (1 bit)
///
///   Dead end: HasValue=0, HasChildren=0 → just 2 bits total (00)
///
///   Otherwise:
///     Size (VarInt): total size of this node + all descendants in bits
///     PrefixLength (VarInt)
///     Prefix (PrefixLength bits)
///     Value (64 bits) - if HasValue
///     Left child (recursive) - if HasChildren
///     Right child (recursive) - if HasChildren
/// </summary>
public readonly struct FlatTrieNode
{
    public bool HasValue { get; init; }
    public bool HasChildren { get; init; }
    public int Size { get; init; }
    public int PrefixLength { get; init; }
    public BitPrefix Prefix { get; init; }
    public long Value { get; init; }

    /// <summary>
    /// Position in the buffer where this node starts.
    /// </summary>
    public int StartBitPosition { get; init; }

    /// <summary>
    /// Position in the buffer where the left child starts (if HasChildren).
    /// </summary>
    public int LeftChildBitPosition { get; init; }

    /// <summary>
    /// Position in the buffer where the right child starts (if HasChildren).
    /// This is calculated by reading the left child's size.
    /// </summary>
    public int RightChildBitPosition { get; init; }

    public bool IsDeadEnd => !HasValue && !HasChildren;

    /// <summary>
    /// Calculate the header size (everything before children).
    /// </summary>
    public int HeaderBitCount => IsDeadEnd ? 2 : CalculateHeaderBitCount();

    private int CalculateHeaderBitCount()
    {
        int bits = 2; // Flags
        bits += VarInt.GetEncodedBitCount(Size);
        bits += VarInt.GetEncodedBitCount(PrefixLength);
        bits += PrefixLength;
        if (HasValue) bits += 64;
        return bits;
    }

    /// <summary>
    /// Read a node from the buffer at the specified bit position.
    /// </summary>
    public static FlatTrieNode Read(ReadOnlySpan<uint> buffer, int bitPosition)
    {
        var reader = new BitArrayReader(buffer, bitPosition);
        return Read(ref reader, bitPosition);
    }

    /// <summary>
    /// Read a node from a reader.
    /// </summary>
    public static FlatTrieNode Read(ref BitArrayReader reader, int startBitPosition)
    {
        bool hasValue = reader.ReadBit();
        bool hasChildren = reader.ReadBit();

        // Dead end
        if (!hasValue && !hasChildren)
        {
            return new FlatTrieNode
            {
                HasValue = false,
                HasChildren = false,
                Size = 2,
                PrefixLength = 0,
                Prefix = BitPrefix.Empty,
                Value = 0,
                StartBitPosition = startBitPosition,
                LeftChildBitPosition = 0,
                RightChildBitPosition = 0
            };
        }

        int size = VarInt.Read(ref reader);
        int prefixLength = VarInt.Read(ref reader);

        // Read prefix in chunks (BitPrefix max is 32 bits)
        BitPrefix prefix = BitPrefix.Empty;
        if (prefixLength > 0 && prefixLength <= 32)
        {
            prefix = reader.ReadPrefix(prefixLength);
        }
        else if (prefixLength > 32)
        {
            // For longer prefixes, we just skip them during node reading
            // The actual prefix matching will be done separately
            reader.Skip(prefixLength);
        }

        long value = 0;
        if (hasValue)
        {
            // Read 64-bit value in two 32-bit chunks
            uint low = reader.ReadBits(32);
            uint high = reader.ReadBits(32);
            value = (long)low | ((long)high << 32);
        }

        int leftChildPos = hasChildren ? reader.BitPosition : 0;
        int rightChildPos = 0;

        if (hasChildren)
        {
            // To get right child position, we need to read left child's size
            // First, read the left child's flags
            bool leftHasValue = reader.ReadBit();
            bool leftHasChildren = reader.ReadBit();

            if (!leftHasValue && !leftHasChildren)
            {
                // Left is dead end, 2 bits
                rightChildPos = leftChildPos + 2;
            }
            else
            {
                // Read left child's size
                int leftSize = VarInt.Read(ref reader);
                // Seek back to left child start
                reader.Seek(leftChildPos);
                rightChildPos = leftChildPos + leftSize;
            }
        }

        return new FlatTrieNode
        {
            HasValue = hasValue,
            HasChildren = hasChildren,
            Size = size,
            PrefixLength = prefixLength,
            Prefix = prefix,
            Value = value,
            StartBitPosition = startBitPosition,
            LeftChildBitPosition = leftChildPos,
            RightChildBitPosition = rightChildPos
        };
    }

    /// <summary>
    /// Write a dead end marker.
    /// </summary>
    public static void WriteDeadEnd(ref BitArrayWriter writer)
    {
        writer.WriteBits(0, 2); // HasValue=0, HasChildren=0
    }

    /// <summary>
    /// Calculate the size of a dead end.
    /// </summary>
    public static int DeadEndSize => 2;

    /// <summary>
    /// Calculate the total size needed to write a node (without children).
    /// </summary>
    public static int CalculateNodeSize(bool hasValue, bool hasChildren, int prefixLength, int childrenSize = 0)
    {
        if (!hasValue && !hasChildren)
            return DeadEndSize; // Dead end

        // Size field itself - we need to calculate this iteratively
        // since the size field's encoding depends on the total size
        int sizeWithoutSizeField = 2; // Flags
        sizeWithoutSizeField += VarInt.GetEncodedBitCount(prefixLength);
        sizeWithoutSizeField += prefixLength;
        if (hasValue) sizeWithoutSizeField += 64;
        sizeWithoutSizeField += childrenSize;

        // Now figure out size field encoding
        // Try each class until we find one that works
        for (int classCode = 0; classCode <= 3; classCode++)
        {
            int sizeFieldBits = 2 + VarInt.BitWidths[classCode];
            int totalWithSize = sizeWithoutSizeField + sizeFieldBits;

            if (totalWithSize <= VarInt.MaxValues[classCode] || classCode == 3)
            {
                return totalWithSize;
            }
        }

        // Should never reach here
        return sizeWithoutSizeField + 20; // Class 3
    }

    /// <summary>
    /// Write a node to the buffer.
    /// </summary>
    public static void Write(ref BitArrayWriter writer, bool hasValue, bool hasChildren,
        int size, ReadOnlySpan<uint> prefixBacking, int prefixOffset, int prefixLength, long value)
    {
        // Flags
        writer.WriteBit(hasValue);
        writer.WriteBit(hasChildren);

        if (!hasValue && !hasChildren)
            return; // Dead end, done

        // Size
        VarInt.Write(ref writer, size);

        // Prefix length and bits
        VarInt.Write(ref writer, prefixLength);
        if (prefixLength > 0)
        {
            // Write prefix bits
            var prefixReader = new BitArrayReader(prefixBacking, prefixOffset);
            for (int i = 0; i < prefixLength; i++)
            {
                writer.WriteBit(prefixReader.ReadBit());
            }
        }

        // Value
        if (hasValue)
        {
            writer.WriteBits((uint)(value & 0xFFFFFFFF), 32);
            writer.WriteBits((uint)(value >> 32), 32);
        }

        // Children are written separately
    }

    /// <summary>
    /// Write a node with a BitPrefix.
    /// </summary>
    public static void Write(ref BitArrayWriter writer, bool hasValue, bool hasChildren,
        int size, BitPrefix prefix, long value)
    {
        // Flags
        writer.WriteBit(hasValue);
        writer.WriteBit(hasChildren);

        if (!hasValue && !hasChildren)
            return; // Dead end, done

        // Size
        VarInt.Write(ref writer, size);

        // Prefix length and bits
        VarInt.Write(ref writer, prefix.Length);
        if (prefix.Length > 0)
        {
            writer.WritePrefix(prefix);
        }

        // Value
        if (hasValue)
        {
            writer.WriteBits((uint)(value & 0xFFFFFFFF), 32);
            writer.WriteBits((uint)(value >> 32), 32);
        }

        // Children are written separately
    }
}
