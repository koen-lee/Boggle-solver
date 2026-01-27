using System.Collections;
using System.Runtime.CompilerServices;

namespace BoggleSolverConsole.Bits;

/// <summary>
/// A compact bit sequence stored in a uint (max 24 bits).
/// Replaces BitArray for prefix storage, avoiding heap allocation.
/// Bits are stored MSB-first: index 0 is at bit 31, index 1 at bit 30, etc.
/// </summary>
public readonly struct BitPrefix
{
    public const int MaxLength = 24;

    private readonly uint _bits;
    private readonly byte _length;

    public int Length => _length;

    public static BitPrefix Empty => default;

    /// <summary>
    /// Get bit at specified index (0 = MSB).
    /// </summary>
    public bool this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (_bits & (0x80000000u >> index)) != 0;
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
                bits |= 0x80000000u >> i;
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
        // Shift left to remove bits before start, keeping MSB alignment
        uint bits = _bits << start;
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
            bits |= 0x80000000u >> _length;
        return new BitPrefix(bits, (byte)(_length + 1));
    }

    /// <summary>
    /// Append another BitPrefix and return the combined prefix.
    /// </summary>
    public BitPrefix Append(BitPrefix other)
    {
        int newLength = _length + other._length;
        if (newLength > MaxLength)
            throw new InvalidOperationException($"Combined length {newLength} exceeds max {MaxLength}");

        // Shift other's bits right to position after our bits
        uint bits = _bits | (other._bits >> _length);
        return new BitPrefix(bits, (byte)newLength);
    }

    /// <summary>
    /// Check if the bits starting at index match a BitArray starting at arrayIndex.
    /// Returns the number of bits that match before a mismatch, or the full length if all match.
    /// </summary>
    public int MatchLength(BitArray array, int arrayIndex)
    {
        int maxMatch = Math.Min(_length, array.Length - arrayIndex);
        for (int i = 0; i < maxMatch; i++)
        {
            if (this[i] != array[arrayIndex + i])
                return i;
        }
        return maxMatch;
    }
}
