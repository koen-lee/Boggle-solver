using System.Collections;
using System.Runtime.CompilerServices;

namespace BoggleSolverConsole.Bits;

/// <summary>
/// A compact immutable struct bit sequence stored in a uint (max 32 bits).
/// Replaces BitArray for prefix storage, avoiding heap allocation.
/// Bits are stored LSB-first: index 0 is at bit 0, index 1 at bit 1, etc.
/// This matches the natural byte ordering used by BitArray and UTF-8.
/// </summary>
public readonly struct BitPrefix
{
    public const int MaxLength = 32;

    private readonly uint _bits;
    private readonly byte _length;

    public int Length => _length;
    public uint Bits => _bits;

    public static BitPrefix Empty => default;

    public static BitPrefix FromBools(params bool[] bits)
    {
        return Empty.Append(bits);
    }

    public static BitPrefix FromBits(uint bits, int length)
    {
        if (length > MaxLength)
            throw new ArgumentOutOfRangeException(nameof(length), $"Length {length} exceeds max {MaxLength}");
        // Mask off any bits beyond length
        uint mask = length == 32 ? uint.MaxValue : (1u << length) - 1;
        return new BitPrefix(bits & mask, (byte)length);
    }

    /// <summary>
    /// Get bit at specified index (0 = LSB).
    /// </summary>
    public bool this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (_bits & (1u << index)) != 0;
    }

    private BitPrefix(uint bits, byte length)
    {
        _bits = bits;
        _length = length;
    }

    /// <summary>
    /// Create a BitPrefix from a slice of a BitArray.
    /// </summary>
    public static BitPrefix FromBitArray(BitArray source, int start, int length)
    {
        if (length == 0)
            return Empty;
        if (length > MaxLength)
            throw new ArgumentOutOfRangeException(nameof(length), $"Length {length} exceeds max {MaxLength}");

        uint bits = 0;
        for (int i = 0; i < length; i++)
        {
            if (source[start + i])
                bits |= 1u << i;
        }
        return new BitPrefix(bits, (byte)length);
    }

    /// <summary>
    /// Create a slice of this BitPrefix.
    /// </summary>
    public BitPrefix Slice(int start, int length)
    {
        if (length == 0)
            return Empty;
        if (start + length > _length)
            throw new ArgumentOutOfRangeException(nameof(length), $"Slice [{start}..{start + length}) exceeds source length {_length}");

        // Shift right to remove bits before start, mask to keep only length bits
        uint bits = (_bits >> start) & ((1u << length) - 1);
        return new BitPrefix(bits, (byte)length);
    }

    /// <summary>
    /// Append a single bit and return the new prefix.
    /// </summary>
    public BitPrefix Append(bool bit)
    {
        if (_length >= MaxLength)
            throw new InvalidOperationException($"Cannot append to prefix at max length {MaxLength}");

        uint bits = _bits;
        if (bit)
            bits |= 1u << _length;
        return new BitPrefix(bits, (byte)(_length + 1));
    }

    /// <summary>
    /// Append bits and return the new prefix.
    /// First appends bitsToAppend[0], then bitsToAppend[1], etc.
    /// </summary>
    public BitPrefix Append(params bool[] bitsToAppend)
    {
        if (_length + bitsToAppend.Length > MaxLength)
            throw new InvalidOperationException($"Cannot append to prefix at max length {MaxLength}");

        uint bits = _bits;
        for (int i = 0; i < bitsToAppend.Length; i++)
        {
            if (bitsToAppend[i])
                bits |= 1u << (_length + i);
        }
        return new BitPrefix(bits, (byte)(_length + bitsToAppend.Length));
    }

    /// <summary>
    /// Append another BitPrefix and return the combined prefix.
    /// </summary>
    public BitPrefix Append(BitPrefix other)
    {
        int newLength = _length + other._length;
        if (newLength > MaxLength)
            throw new InvalidOperationException($"Combined length {newLength} exceeds max {MaxLength}");

        // Shift other's bits left to position after our bits
        uint bits = _bits | (other._bits << _length);
        return new BitPrefix(bits, (byte)newLength);
    }
}
