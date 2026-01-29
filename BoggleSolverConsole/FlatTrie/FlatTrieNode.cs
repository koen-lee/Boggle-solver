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
public class FlatTrieNode
{
    private long value;


    public FlatTrieNode(uint[] backing, int startBitPosition)
    {
        Backing = backing;

        InitialBitPosition = startBitPosition;
        var reader = new BitArrayReader(Backing, InitialBitPosition);
        HasValue = reader.ReadBit();
        HasChildren = reader.ReadBit();
        if (IsDeadEnd)
        {
            OriginalSize = 2;
            PrefixLength = 0;
            Value = 0;
            return;
        }
        OriginalSize = VarInt.Read(ref reader);
        PrefixLength = VarInt.Read(ref reader);
    }

    public bool HasValue { get; private set; }
    public bool HasChildren { get; private set; }
    public int OriginalSize { get; private set; }
    public int PrefixLength { get; private set; }
    public long Value
    {
        get => value; private set
        {
            this.value = value;
            IsDirty = true; // but size doesn't change
        }
    }

    public bool IsDirty { get; private set; } = false;
    /// <summary>
    /// Position in the buffer where this node starts on node creation.
    /// May differ from the position when writing if a node before it is modified.
    /// </summary>
    public required int InitialBitPosition { get; init; }

    public required uint[] Backing { get; init; }

    public bool IsDeadEnd => !HasValue && !HasChildren;

    /// <summary>
    /// Calculate the header size (everything before children).
    /// </summary>
    public int HeaderBitCount => IsDeadEnd ? 2 : CalculateHeaderBitCount();

    private int CalculateHeaderBitCount()
    {
        int bits = 2; // Flags
        bits += VarInt.GetEncodedBitCount(OriginalSize);
        bits += VarInt.GetEncodedBitCount(PrefixLength);
        bits += PrefixLength;
        if (HasValue) bits += 64;
        return bits;
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
    /// Uses fixed 20-bit size field to avoid rebuilds.
    /// </summary>
    public static int CalculateNodeSize(bool hasValue, bool hasChildren, int prefixLength, int childrenSize = 0)
    {
        if (!hasValue && !hasChildren)
            return DeadEndSize; // Dead end

        // Fixed-size encoding: always use class 3 (20 bits) for size field
        // This eliminates the need for rebuilds when sizes grow
        int size = 2; // Flags
        size += VarInt.SizeFieldBits; // Fixed 20-bit size field
        size += VarInt.GetEncodedBitCount(prefixLength);
        size += prefixLength;
        if (hasValue) size += 64;
        size += childrenSize;

        return size;
    }

    /// <summary>
    /// Writes this node to the buffer.
    /// </summary>
    public void Write(ref BitArrayWriter writer)
    {
        // Flags
        writer.WriteBit(HasValue);
        writer.WriteBit(HasChildren);

        if (IsDeadEnd)
            return; // Dead end, done

        // Size
        VarInt.Write(ref writer, OriginalSize);
        // Prefix length and bits
        VarInt.Write(ref writer, PrefixLength);
        if (PrefixLength > 0)
        {
            // Write prefix bits
            var prefix = ReadOnlyBitString.Wrap(Backing).Slice(InitialBitPosition + 2 + VarInt.GetEncodedBitCount(OriginalSize), PrefixLength);
            writer.WriteBitString(ref prefix);
        }

        // Value
        if (HasValue)
        {
            writer.WriteBits((uint)(Value & 0xFFFFFFFF), 32);
            writer.WriteBits((uint)(Value >> 32), 32);
        }
        /*
                if( HasChildren )
                {
                    LeftChild.Write(ref writer);
                    RightChild.Write(ref writer);
                }*/
    }
}
