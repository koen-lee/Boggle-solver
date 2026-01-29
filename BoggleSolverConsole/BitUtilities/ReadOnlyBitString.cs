using System.Numerics;
using System.Runtime.CompilerServices;

namespace BitUtilities;

/// <summary>
/// A readonly struct representing a view into a bit sequence.
/// Supports zero-copy slicing by storing offset and length into a shared backing array.
/// Bits are stored LSB-first to match BitPrefix and BitArray conventions.
/// </summary>
public readonly ref struct ReadOnlyBitString
{
    private readonly ReadOnlySpan<uint> _backing;  // Shared backing array (null = empty)
    private readonly int _bitOffset;   // Starting bit position in backing
    private readonly int _bitLength;   // Number of bits in this view

    public int Length => _bitLength;
    public bool IsEmpty => _bitLength == 0;

    public static ReadOnlyBitString Empty => default;

    /// <summary>
    /// Wrap an existing uint[] array without copying.
    /// The BitString will view all bits from offset 0 to bitLength.
    /// </summary>
    public static ReadOnlyBitString Wrap(ReadOnlySpan<uint> backing, int bitLength)
    {
        if (backing == null || bitLength == 0)
            return Empty;
        if (bitLength > backing.Length * 32)
            throw new ArgumentOutOfRangeException(nameof(bitLength),
                $"Bit length {bitLength} exceeds backing capacity {backing.Length * 32}");
        return new ReadOnlyBitString(backing, 0, bitLength);
    }

    /// <summary>
    /// Wrap an existing uint[] array without copying, using full capacity.
    /// </summary>
    public static ReadOnlyBitString Wrap(ReadOnlySpan<uint> backing)
    {
        if (backing == null || backing.Length == 0)
            return Empty;
        return new ReadOnlyBitString(backing, 0, backing.Length * 32);
    }

    private ReadOnlyBitString(ReadOnlySpan<uint> backing, int bitOffset, int bitLength)
    {
        _backing = backing;
        _bitOffset = bitOffset;
        _bitLength = bitLength;
    }

    /// <summary>
    /// Create BitString from byte array (8 bits per byte, LSB-first).
    /// Used for ASCII encoding where each char = 1 byte.
    /// </summary>
    public static ReadOnlyBitString FromBytes(byte[] bytes)
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

        return new ReadOnlyBitString(backing, 0, bitCount);
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
    public ReadOnlyBitString Slice(int start, int length)
    {
        if (length == 0)
            return Empty;
        if (start < 0 || length < 0 || start + length > _bitLength)
            throw new ArgumentOutOfRangeException(
                nameof(length),
                $"Slice [{start}..{start + length}) exceeds bounds [0..{_bitLength})");

        return new ReadOnlyBitString(_backing, _bitOffset + start, length);
    }

    /// <summary>
    /// Slice from start to end of BitString.
    /// </summary>
    public ReadOnlyBitString Slice(int start)
    {
        return this[start.._bitLength];
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

    /// <summary>
    /// Extract up to 32 bits starting at the given position (relative to this slice).
    /// Returns the bits as a uint, LSB-first.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private uint GetBits(int start, int count)
    {
        int absoluteBitOffset = _bitOffset + start;
        int uintIndex = absoluteBitOffset / 32;
        int bitInUint = absoluteBitOffset % 32;

        // Extract bits from first uint
        uint result = _backing[uintIndex] >> bitInUint;

        // If extraction spans two uints, get remaining bits from next uint
        if (bitInUint + count > 32 && uintIndex + 1 < _backing.Length)
        {
            result |= _backing[uintIndex + 1] << (32 - bitInUint);
        }

        // Mask to only include count bits
        if (count < 32)
        {
            result &= (1u << count) - 1;
        }

        return result;
    }

    /// <summary>
    /// Returns the number of leading bits that match between this and another bit string.
    /// Uses XOR and TrailingZeroCount for efficient uint-at-a-time comparison.
    /// </summary>
    public int CommonPrefixLength(ref ReadOnlyBitString other)
    {
        int maxCompare = Math.Min(_bitLength, other._bitLength);
        if (maxCompare == 0) return 0;

        int compared = 0;

        // Compare 32 bits at a time where possible
        while (compared + 32 <= maxCompare)
        {
            uint a = GetBits(compared, 32);
            uint b = other.GetBits(compared, 32);
            uint xor = a ^ b;

            if (xor != 0)
            {
                // Found a mismatch - return position of first differing bit
                return compared + BitOperations.TrailingZeroCount(xor);
            }

            compared += 32;
        }

        // Handle remaining bits (less than 32)
        int remaining = maxCompare - compared;
        if (remaining > 0)
        {
            uint a = GetBits(compared, remaining);
            uint b = other.GetBits(compared, remaining);
            uint xor = a ^ b;

            if (xor != 0)
            {
                int mismatchPos = BitOperations.TrailingZeroCount(xor);
                return compared + Math.Min(mismatchPos, remaining);
            }
        }

        return maxCompare;
    }
}
