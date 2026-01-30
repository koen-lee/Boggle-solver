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

    /// <summary>
    /// Shift bits left (shrink) by delta bits.
    /// Bits from [fromBitPos, fromBitPos+bitsToMove) are moved to [fromBitPos-delta, fromBitPos-delta+bitsToMove).
    /// Bits before (fromBitPos-delta) are preserved.
    /// Uses word-level barrel shift for efficiency.
    /// </summary>
    public static void ShiftBitsLeft(Span<uint> buffer, int fromBitPos, int bitsToMove, int delta)
    {
        if (bitsToMove <= 0 || delta <= 0)
            return;

        int dstStartBit = fromBitPos - delta;
        int srcBitInWord = fromBitPos & 31;
        int dstBitInWord = dstStartBit & 31;
        int rot = delta & 31;
        int wordShift = delta >> 5;

        // Fast path 1: both source and dest are word-aligned - pure word copy
        if (srcBitInWord == 0 && dstBitInWord == 0)
        {
            int srcStartWord = fromBitPos >> 5;
            int srcEndWord = (fromBitPos + bitsToMove - 1) >> 5;

            for (int srcWord = srcStartWord; srcWord <= srcEndWord; srcWord++)
            {
                int dstWord = srcWord - wordShift;
                if (dstWord >= 0)
                    buffer[dstWord] = buffer[srcWord];
            }
            return;
        }

        // Fast path 2: delta is word-aligned (common case like 64-bit value removal)
        // Source and dest have same bit offset, so middle words can be copied directly
        if (rot == 0)
        {
            int dstStartWord = dstStartBit >> 5;
            int dstEndWord = (dstStartBit + bitsToMove - 1) >> 5;
            int bitOffset = dstBitInWord; // same as srcBitInWord

            for (int dstWord = dstStartWord; dstWord <= dstEndWord; dstWord++)
            {
                int srcWord = dstWord + wordShift;
                bool isFirstWord = (dstWord == dstStartWord) && (bitOffset != 0);
                bool isLastWord = (dstWord == dstEndWord);
                int lastBitInRange = (dstStartBit + bitsToMove - 1) & 31;

                if (isFirstWord || (isLastWord && lastBitInRange != 31))
                {
                    // Partial word - need to preserve some bits
                    int startBit = isFirstWord ? bitOffset : 0;
                    int endBit = isLastWord ? lastBitInRange + 1 : 32;
                    uint mask = ((1u << (endBit - startBit)) - 1) << startBit;
                    if (endBit - startBit == 32) mask = uint.MaxValue;

                    uint srcValue = (srcWord < buffer.Length) ? buffer[srcWord] : 0;
                    buffer[dstWord] = (buffer[dstWord] & ~mask) | (srcValue & mask);
                }
                else
                {
                    // Complete word - direct copy
                    buffer[dstWord] = buffer[srcWord];
                }
            }
            return;
        }

        // General case: non-word-aligned delta, use barrel shift with boundary preservation
        int dstEndBit = dstStartBit + bitsToMove;
        int dstStartWordGen = dstStartBit >> 5;
        int dstEndWordGen = (dstEndBit - 1) >> 5;

        // Pre-save boundary words for restoration after loop
        uint savedFirstWord = buffer[dstStartWordGen];
        uint savedLastWord = buffer[dstEndWordGen];

        // Pre-load first lowBits (will become highBits after first iteration)
        int firstSrcWordLow = dstStartWordGen + wordShift;
        uint lowBits = (firstSrcWordLow >= 0 && firstSrcWordLow < buffer.Length) ? buffer[firstSrcWordLow] : 0;

        // Tight loop - write full words, fix boundaries after
        for (int dstWord = dstStartWordGen; dstWord <= dstEndWordGen; dstWord++)
        {
            int srcWordHigh = dstWord + wordShift + 1;
            uint highBits = (srcWordHigh >= 0 && srcWordHigh < buffer.Length) ? buffer[srcWordHigh] : 0;

            // 64-bit barrel shift: combine two 32-bit words and extract middle 32 bits
            ulong combined = ((ulong)highBits << 32) | lowBits;
            buffer[dstWord] = (uint)(combined >> rot);

            lowBits = highBits;
        }

        // Fix up boundary words - restore bits outside the write range
        int firstWordOffset = dstStartBit & 31;
        int lastWordEndBit = ((dstEndBit - 1) & 31) + 1;

        if (dstStartWordGen == dstEndWordGen)
        {
            // Single word case - preserve bits outside [firstWordOffset, lastWordEndBit)
            uint writeMask = ((1u << (lastWordEndBit - firstWordOffset)) - 1) << firstWordOffset;
            buffer[dstStartWordGen] = (savedFirstWord & ~writeMask) | (buffer[dstStartWordGen] & writeMask);
        }
        else
        {
            if (firstWordOffset != 0)
            {
                uint preserveMask = (1u << firstWordOffset) - 1;
                buffer[dstStartWordGen] = (savedFirstWord & preserveMask) | (buffer[dstStartWordGen] & ~preserveMask);
            }
            if (lastWordEndBit != 32)
            {
                uint preserveMask = uint.MaxValue << lastWordEndBit;
                buffer[dstEndWordGen] = (savedLastWord & preserveMask) | (buffer[dstEndWordGen] & ~preserveMask);
            }
        }
    }

    /// <summary>
    /// Shift bits right (expand) by delta bits.
    /// Bits from [fromBitPos, fromBitPos+bitsToMove) are moved to [fromBitPos+delta, fromBitPos+delta+bitsToMove).
    /// The gap [fromBitPos, fromBitPos+delta) is left unchanged (caller will overwrite).
    /// Uses word-level barrel shift for efficiency. Works backwards to avoid overwriting source.
    /// </summary>
    public static void ShiftBitsRight(Span<uint> buffer, int fromBitPos, int bitsToMove, int delta)
    {
        if (bitsToMove <= 0 || delta <= 0)
            return;

        int dstStartBit = fromBitPos + delta;
        int srcBitInWord = fromBitPos & 31;
        int dstBitInWord = dstStartBit & 31;
        int rot = delta & 31;
        int wordShift = delta >> 5;

        // Fast path 1: both source and dest are word-aligned - pure word copy
        if (srcBitInWord == 0 && dstBitInWord == 0)
        {
            int srcStartWord = fromBitPos >> 5;
            int srcEndWord = (fromBitPos + bitsToMove - 1) >> 5;

            // Copy backwards to avoid overwriting source
            for (int srcWord = srcEndWord; srcWord >= srcStartWord; srcWord--)
                buffer[srcWord + wordShift] = buffer[srcWord];
            return;
        }

        // Fast path 2: delta is word-aligned (common case like 64-bit value insertion)
        // Source and dest have same bit offset, so middle words can be copied directly
        if (rot == 0)
        {
            int dstStartWord = dstStartBit >> 5;
            int dstEndWord = (dstStartBit + bitsToMove - 1) >> 5;
            int bitOffset = dstBitInWord;

            // Copy backwards to avoid overwriting source
            for (int dstWord = dstEndWord; dstWord >= dstStartWord; dstWord--)
            {
                int srcWord = dstWord - wordShift;
                bool isFirstWord = (dstWord == dstStartWord) && (bitOffset != 0);
                bool isLastWord = (dstWord == dstEndWord);
                int lastBitInRange = (dstStartBit + bitsToMove - 1) & 31;

                if (isFirstWord || (isLastWord && lastBitInRange != 31))
                {
                    // Partial word - need to preserve some bits
                    int startBit = isFirstWord ? bitOffset : 0;
                    int endBit = isLastWord ? lastBitInRange + 1 : 32;
                    uint mask = ((1u << (endBit - startBit)) - 1) << startBit;
                    if (endBit - startBit == 32) mask = uint.MaxValue;

                    uint srcValue = (srcWord >= 0 && srcWord < buffer.Length) ? buffer[srcWord] : 0;
                    buffer[dstWord] = (buffer[dstWord] & ~mask) | (srcValue & mask);
                }
                else
                {
                    // Complete word - direct copy
                    buffer[dstWord] = buffer[srcWord];
                }
            }
            return;
        }

        // General case: non-word-aligned delta, use barrel shift
        int dstEndBit = dstStartBit + bitsToMove;
        int dstStartWordGen = dstStartBit >> 5;
        int dstEndWordGen = (dstEndBit - 1) >> 5;

        // Pre-save boundary words for restoration after loop
        uint savedFirstWord = buffer[dstStartWordGen];
        uint savedLastWord = buffer[dstEndWordGen];

        // Pre-load first highBits (will become lowBits after first iteration)
        int firstSrcWordHigh = dstEndWordGen - wordShift;
        uint highBits = (firstSrcWordHigh >= 0 && firstSrcWordHigh < buffer.Length) ? buffer[firstSrcWordHigh] : 0;

        // Process backwards to avoid overwriting source - tight loop, fix boundaries after
        for (int dstWord = dstEndWordGen; dstWord >= dstStartWordGen; dstWord--)
        {
            int srcWordLow = dstWord - wordShift - 1;
            uint lowBits = (srcWordLow >= 0 && srcWordLow < buffer.Length) ? buffer[srcWordLow] : 0;

            // 64-bit barrel shift: combine two 32-bit words and extract middle 32 bits
            ulong combined = ((ulong)highBits << 32) | lowBits;
            buffer[dstWord] = (uint)(combined >> (32 - rot));

            highBits = lowBits;
        }

        // Fix up boundary words - restore bits outside the write range
        int firstWordOffset = dstStartBit & 31;
        int lastWordEndBit = ((dstEndBit - 1) & 31) + 1;

        if (dstStartWordGen == dstEndWordGen)
        {
            // Single word case - preserve bits outside [firstWordOffset, lastWordEndBit)
            uint writeMask = ((1u << (lastWordEndBit - firstWordOffset)) - 1) << firstWordOffset;
            buffer[dstStartWordGen] = (savedFirstWord & ~writeMask) | (buffer[dstStartWordGen] & writeMask);
        }
        else
        {
            if (firstWordOffset != 0)
            {
                uint preserveMask = (1u << firstWordOffset) - 1;
                buffer[dstStartWordGen] = (savedFirstWord & preserveMask) | (buffer[dstStartWordGen] & ~preserveMask);
            }
            if (lastWordEndBit != 32)
            {
                uint preserveMask = uint.MaxValue << lastWordEndBit;
                buffer[dstEndWordGen] = (savedLastWord & preserveMask) | (buffer[dstEndWordGen] & ~preserveMask);
            }
        }
    }
}
