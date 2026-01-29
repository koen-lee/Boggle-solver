using System.Runtime.CompilerServices;

namespace BitUtilities;

/// <summary>
/// Writes bits to a uint[] array at a specified bit position.
/// Unlike BitWriter (which writes to a Stream), this writes directly to memory
/// and supports random-access positioning.
/// </summary>
public ref struct BitArrayWriter
{
    private readonly Span<uint> _backing;
    private int _bitPosition;

    public int BitPosition => _bitPosition;
    public int Capacity => _backing.Length * 32;

    public BitArrayWriter(Span<uint> backing)
    {
        _backing = backing;
        _bitPosition = 0;
    }

    public BitArrayWriter(Span<uint> backing, int startBitPosition)
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
    /// Write a single bit.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBit(bool value)
    {
        int uintIndex = _bitPosition / 32;
        int bitInUint = _bitPosition % 32;

        if (value)
            _backing[uintIndex] |= 1u << bitInUint;
        else
            _backing[uintIndex] &= ~(1u << bitInUint);

        _bitPosition++;
    }

    /// <summary>
    /// Write multiple bits from a uint value (LSB first).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBits(uint value, int bitCount)
    {
        if (bitCount == 0) return;
        if (bitCount > 32)
            throw new ArgumentOutOfRangeException(nameof(bitCount), "Cannot write more than 32 bits at once");

        int uintIndex = _bitPosition / 32;
        int bitInUint = _bitPosition % 32;

        // Mask the value to only include bitCount bits
        uint mask = bitCount == 32 ? uint.MaxValue : (1u << bitCount) - 1;
        value &= mask;

        // Clear the target bits and set new value
        uint clearMask = ~(mask << bitInUint);
        _backing[uintIndex] = (_backing[uintIndex] & clearMask) | (value << bitInUint);

        // Handle overflow into next uint
        if (bitInUint + bitCount > 32 && uintIndex + 1 < _backing.Length)
        {
            int bitsInFirst = 32 - bitInUint;
            int bitsInSecond = bitCount - bitsInFirst;
            uint secondMask = (1u << bitsInSecond) - 1;
            uint secondClearMask = ~secondMask;
            _backing[uintIndex + 1] = (_backing[uintIndex + 1] & secondClearMask) | (value >> bitsInFirst);
        }

        _bitPosition += bitCount;
    }

    /// <summary>
    /// Write a BitPrefix.
    /// </summary>
    public void WritePrefix(BitPrefix prefix)
    {
        WriteBits(prefix.Bits, prefix.Length);
    }

    /// <summary>
    /// Write bits from a BitString.
    /// </summary>
    public void WriteBitString(BitString bits)
    {
        // Write in 32-bit chunks for efficiency
        int remaining = bits.Length;
        int sourceOffset = 0;

        while (remaining >= 32)
        {
            uint chunk = bits.ToBitPrefix(sourceOffset, 32).Bits;
            WriteBits(chunk, 32);
            sourceOffset += 32;
            remaining -= 32;
        }

        if (remaining > 0)
        {
            uint chunk = bits.ToBitPrefix(sourceOffset, remaining).Bits;
            WriteBits(chunk, remaining);
        }
    }
    
    /// <summary>
    /// Write bits from a BitString.
    /// </summary>
    public void WriteBitString(ref ReadOnlyBitString bits)
    {
        // Write in 32-bit chunks for efficiency
        int remaining = bits.Length;
        int sourceOffset = 0;

        while (remaining >= 32)
        {
            uint chunk = bits.ToBitPrefix(sourceOffset, 32).Bits;
            WriteBits(chunk, 32);
            sourceOffset += 32;
            remaining -= 32;
        }

        if (remaining > 0)
        {
            uint chunk = bits.ToBitPrefix(sourceOffset, remaining).Bits;
            WriteBits(chunk, remaining);
        }
    }

    /// <summary>
    /// Write a 64-bit long value (LSB first).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteLong(long value)
    {
        WriteBits((uint)(value & 0xFFFFFFFF), 32);
        WriteBits((uint)(value >> 32), 32);
    }
}
