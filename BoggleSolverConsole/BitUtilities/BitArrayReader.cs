using System.Runtime.CompilerServices;

namespace BitUtilities;

/// <summary>
/// Reads bits from a uint[] array at a specified bit position.
/// Unlike BitReader (which reads from a Stream), this reads directly from memory
/// and supports random-access positioning.
/// </summary>
public ref struct BitArrayReader
{
    private readonly ReadOnlySpan<uint> _backing;
    private int _bitPosition;

    public int BitPosition => _bitPosition;
    public int Capacity => _backing.Length * 32;

    public BitArrayReader(ReadOnlySpan<uint> backing)
    {
        _backing = backing;
        _bitPosition = 0;
    }

    public BitArrayReader(ReadOnlySpan<uint> backing, int startBitPosition)
    {
        _backing = backing;
        _bitPosition = startBitPosition;
    }

    /// <summary>
    /// Seek to a specific bit position.
    /// </summary>
    public void Seek(int bitPosition)
    {
        if (bitPosition < 0 || bitPosition > Capacity)
            throw new ArgumentOutOfRangeException(nameof(bitPosition));
        _bitPosition = bitPosition;
    }

    /// <summary>
    /// Skip forward by the specified number of bits.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Skip(int bitCount)
    {
        _bitPosition += bitCount;
    }

    /// <summary>
    /// Read a single bit.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ReadBit()
    {
        int uintIndex = _bitPosition / 32;
        int bitInUint = _bitPosition % 32;
        _bitPosition++;
        return (_backing[uintIndex] & (1u << bitInUint)) != 0;
    }

    /// <summary>
    /// Read multiple bits into a uint value (LSB first).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ReadBits(int bitCount)
    {
        if (bitCount == 0) return 0;
        if (bitCount > 32)
            throw new ArgumentOutOfRangeException(nameof(bitCount), "Cannot read more than 32 bits at once");

        int uintIndex = _bitPosition / 32;
        int bitInUint = _bitPosition % 32;

        // Extract bits from first uint
        uint result = _backing[uintIndex] >> bitInUint;

        // If extraction spans two uints, get remaining bits from next uint
        if (bitInUint + bitCount > 32 && uintIndex + 1 < _backing.Length)
        {
            result |= _backing[uintIndex + 1] << (32 - bitInUint);
        }

        // Mask to only include bitCount bits
        if (bitCount < 32)
        {
            result &= (1u << bitCount) - 1;
        }

        _bitPosition += bitCount;
        return result;
    }

    /// <summary>
    /// Read a BitPrefix of the specified length.
    /// </summary>
    public BitPrefix ReadPrefix(int bitCount)
    {
        return BitPrefix.FromBits(ReadBits(bitCount), bitCount);
    }

    /// <summary>
    /// Peek at the next bit without advancing the position.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool PeekBit()
    {
        int uintIndex = _bitPosition / 32;
        int bitInUint = _bitPosition % 32;
        return (_backing[uintIndex] & (1u << bitInUint)) != 0;
    }

    /// <summary>
    /// Read a 64-bit long value (LSB first).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long ReadLong()
    {
        uint low = ReadBits(32);
        uint high = ReadBits(32);
        return low | ((long)high << 32);
    }
}
