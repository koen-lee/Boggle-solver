using System.Runtime.CompilerServices;

namespace BitUtilities;

/// <summary>
/// A readonly struct representing a view into a bit sequence.
/// Supports zero-copy slicing by storing offset and length into a shared backing array.
/// Bits are stored LSB-first to match BitPrefix and BitArray conventions.
/// </summary>
public readonly struct BitString
{
    private readonly uint[] _backing;  // Shared backing array (null = empty)
    private readonly int _bitOffset;   // Starting bit position in backing
    private readonly int _bitLength;   // Number of bits in this view

    public int Length => _bitLength;
    public bool IsEmpty => _bitLength == 0;

    public static BitString Empty => default;

    /// <summary>
    /// Wrap an existing uint[] array without copying.
    /// The BitString will view all bits from offset 0 to bitLength.
    /// </summary>
    public static BitString Wrap(uint[] backing, int bitLength)
    {
        if (backing == null || bitLength == 0)
            return Empty;
        if (bitLength > backing.Length * 32)
            throw new ArgumentOutOfRangeException(nameof(bitLength),
                $"Bit length {bitLength} exceeds backing capacity {backing.Length * 32}");
        return new BitString(backing, 0, bitLength);
    }

    /// <summary>
    /// Wrap an existing uint[] array without copying, using full capacity.
    /// </summary>
    public static BitString Wrap(uint[] backing)
    {
        if (backing == null || backing.Length == 0)
            return Empty;
        return new BitString(backing, 0, backing.Length * 32);
    }

    private BitString(uint[] backing, int bitOffset, int bitLength)
    {
        _backing = backing;
        _bitOffset = bitOffset;
        _bitLength = bitLength;
    }

    /// <summary>
    /// Create BitString from byte array (8 bits per byte, LSB-first).
    /// Used for ASCII encoding where each char = 1 byte.
    /// </summary>
    public static BitString FromBytes(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
            return Empty;

        int bitCount = bytes.Length * 8;
        var backing = new uint[(bitCount + 31) / 32];

        // Pack bytes into uints (4 bytes per uint, LSB-first)
        for (int i = 0; i < bytes.Length; i++)
        {
            int uintIndex = i / 4;
            int byteInUint = i % 4;
            backing[uintIndex] |= (uint)bytes[i] << (byteInUint * 8);
        }

        return new BitString(backing, 0, bitCount);
    }

    /// <summary>
    /// Create BitString from bool list (used during enumeration).
    /// </summary>
    public static BitString FromBools(List<bool> bools)
    {
        if (bools == null || bools.Count == 0)
            return Empty;

        int bitCount = bools.Count;
        var backing = new uint[(bitCount + 31) / 32];

        for (int i = 0; i < bools.Count; i++)
        {
            if (bools[i])
            {
                int uintIndex = i / 32;
                int bitInUint = i % 32;
                backing[uintIndex] |= 1u << bitInUint;
            }
        }

        return new BitString(backing, 0, bitCount);
    }

    /// <summary>
    /// Create BitString from int array with explicit bit length.
    /// Used for 5-bit encoding where int[] contains packed bits.
    /// </summary>
    public static BitString FromInts(int[] ints, int bitLength)
    {
        if (ints == null || ints.Length == 0 || bitLength == 0)
            return Empty;

        // Copy int[] to uint[] (reinterpret)
        var backing = new uint[ints.Length];
        for (int i = 0; i < ints.Length; i++)
            backing[i] = (uint)ints[i];

        return new BitString(backing, 0, bitLength);
    }

    /// <summary>
    /// Get bit at index (relative to this slice's start).
    /// </summary>
    public bool this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            int absoluteBitOffset = _bitOffset + index;
            int uintIndex = absoluteBitOffset / 32;
            int bitInUint = absoluteBitOffset % 32;
            return (_backing[uintIndex] & (1u << bitInUint)) != 0;
        }
    }

    /// <summary>
    /// Create a new BitString viewing a subset of the same backing array.
    /// Zero-copy operation - only allocates the struct itself.
    /// </summary>
    public BitString Slice(int start, int length)
    {
        if (length == 0)
            return Empty;
        if (start < 0 || length < 0 || start + length > _bitLength)
            throw new ArgumentOutOfRangeException(
                nameof(length),
                $"Slice [{start}..{start + length}) exceeds bounds [0..{_bitLength})");

        return new BitString(_backing, _bitOffset + start, length);
    }

    /// <summary>
    /// Slice from start to end of BitString.
    /// </summary>
    public BitString Slice(int start)
    {
        return this[start.._bitLength];
    }

    /// <summary>
    /// Convert to ReadOnlyBitString viewing the same bits.
    /// </summary>
    public ReadOnlyBitString AsReadOnly()
    {
        if (_backing == null || _bitLength == 0)
            return ReadOnlyBitString.Empty;
        return ReadOnlyBitString.Wrap(_backing).Slice(_bitOffset, _bitLength);
    }

    /// <summary>
    /// Extract a slice as BitPrefix using efficient bit operations.
    /// Handles extraction spanning 1-2 uints without bit-by-bit iteration.
    /// </summary>
    public BitPrefix ToBitPrefix(int start, int length)
    {
        if (length == 0)
            return BitPrefix.Empty;
        if (length > BitPrefix.MaxLength)
            throw new ArgumentOutOfRangeException(
                nameof(length),
                $"Length {length} exceeds BitPrefix.MaxLength ({BitPrefix.MaxLength})");
        if (start < 0 || start + length > _bitLength)
            throw new ArgumentOutOfRangeException(
                nameof(length),
                $"Range [{start}..{start + length}) exceeds bounds [0..{_bitLength})");

        int absoluteBitOffset = _bitOffset + start;
        int uintIndex = absoluteBitOffset / 32;
        int bitInUint = absoluteBitOffset % 32;

        // Extract bits from first uint
        uint result = _backing[uintIndex] >> bitInUint;

        // If extraction spans two uints, get remaining bits from next uint
        if (bitInUint + length > 32 && uintIndex + 1 < _backing.Length)
        {
            result |= _backing[uintIndex + 1] << (32 - bitInUint);
        }

        return BitPrefix.FromBits(result, length);
    }
}
