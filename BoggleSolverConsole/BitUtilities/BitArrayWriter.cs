using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

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
            int startOffset = dstBitInWord;
            int endOffset = ((dstStartBit + bitsToMove - 1) & 31) + 1;

            // Pre-save boundary words
            uint savedFirst = buffer[dstStartWord];
            uint savedLast = buffer[dstEndWord];

            // Tight loop - direct word copies
            for (int dstWord = dstStartWord; dstWord <= dstEndWord; dstWord++)
            {
                int srcWord = dstWord + wordShift;
                buffer[dstWord] = (srcWord < buffer.Length) ? buffer[srcWord] : 0;
            }

            // Fix up boundary words
            if (dstStartWord == dstEndWord)
            {
                uint writeMask = ((1u << (endOffset - startOffset)) - 1) << startOffset;
                buffer[dstStartWord] = (savedFirst & ~writeMask) | (buffer[dstStartWord] & writeMask);
            }
            else
            {
                if (startOffset != 0)
                {
                    uint preserveMask = (1u << startOffset) - 1;
                    buffer[dstStartWord] = (savedFirst & preserveMask) | (buffer[dstStartWord] & ~preserveMask);
                }
                if (endOffset != 32)
                {
                    uint preserveMask = uint.MaxValue << endOffset;
                    buffer[dstEndWord] = (savedLast & preserveMask) | (buffer[dstEndWord] & ~preserveMask);
                }
            }
            return;
        }

        // General case: non-word-aligned delta, use barrel shift with boundary preservation
        int dstEndBit = dstStartBit + bitsToMove;
        int dstStartWordGen = dstStartBit >> 5;
        int dstEndWordGen = (dstEndBit - 1) >> 5;
        int firstWordOffset = dstStartBit & 31;
        int lastWordEndBit = ((dstEndBit - 1) & 31) + 1;

        // Fast path 3: single word destination - just one barrel shift with mask
        if (dstStartWordGen == dstEndWordGen)
        {
            int srcWordLow = dstStartWordGen + wordShift;
            int srcWordHigh = srcWordLow + 1;
            uint lowBits = (srcWordLow >= 0 && srcWordLow < buffer.Length) ? buffer[srcWordLow] : 0;
            uint highBits = (srcWordHigh >= 0 && srcWordHigh < buffer.Length) ? buffer[srcWordHigh] : 0;

            ulong combined = ((ulong)highBits << 32) | lowBits;
            uint shifted = (uint)(combined >> rot);

            uint writeMask = ((1u << (lastWordEndBit - firstWordOffset)) - 1) << firstWordOffset;
            buffer[dstStartWordGen] = (buffer[dstStartWordGen] & ~writeMask) | (shifted & writeMask);
            return;
        }

        // Multi-word case: pre-save boundary words for restoration after loop
        uint savedFirstWord = buffer[dstStartWordGen];
        uint savedLastWord = buffer[dstEndWordGen];

        // Pre-load first lowBits (will become highBits after first iteration)
        int firstSrcWordLow = dstStartWordGen + wordShift;
        uint lowBitsLoop = (firstSrcWordLow >= 0 && firstSrcWordLow < buffer.Length) ? buffer[firstSrcWordLow] : 0;

        // Tight loop - write full words, fix boundaries after
        for (int dstWord = dstStartWordGen; dstWord <= dstEndWordGen; dstWord++)
        {
            int srcWordHigh = dstWord + wordShift + 1;
            uint highBits = (srcWordHigh >= 0 && srcWordHigh < buffer.Length) ? buffer[srcWordHigh] : 0;

            ulong combined = ((ulong)highBits << 32) | lowBitsLoop;
            buffer[dstWord] = (uint)(combined >> rot);

            lowBitsLoop = highBits;
        }

        // Fix up boundary words - restore bits outside the write range
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

    /// <summary>
    /// Shift bits right (expand) by delta bits.
    /// Bits from [fromBitPos, fromBitPos+bitsToMove) are moved to [fromBitPos+delta, fromBitPos+delta+bitsToMove).
    /// The gap [fromBitPos, fromBitPos+delta) is left unchanged (caller will overwrite).
    /// Uses word-level barrel shift for efficiency. Works backwards to avoid overwriting source.
    /// </summary>
    public static void ShiftBitsRight(Span<uint> buffer, int fromBitPos, int bitsToMove, int delta)
    => ShiftBitsRightSimd(buffer, fromBitPos, bitsToMove, delta);
    public static void ShiftBitsRight_old(Span<uint> buffer, int fromBitPos, int bitsToMove, int delta)
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
            int startOffset = dstBitInWord;
            int endOffset = ((dstStartBit + bitsToMove - 1) & 31) + 1;

            // Pre-save boundary words
            uint savedFirst = buffer[dstStartWord];
            uint savedLast = buffer[dstEndWord];

            // Tight loop - direct word copies (backwards to avoid overwriting source)
            for (int dstWord = dstEndWord; dstWord >= dstStartWord; dstWord--)
            {
                int srcWord = dstWord - wordShift;
                buffer[dstWord] = (srcWord >= 0 && srcWord < buffer.Length) ? buffer[srcWord] : 0;
            }

            // Fix up boundary words
            if (dstStartWord == dstEndWord)
            {
                uint writeMask = ((1u << (endOffset - startOffset)) - 1) << startOffset;
                buffer[dstStartWord] = (savedFirst & ~writeMask) | (buffer[dstStartWord] & writeMask);
            }
            else
            {
                if (startOffset != 0)
                {
                    uint preserveMask = (1u << startOffset) - 1;
                    buffer[dstStartWord] = (savedFirst & preserveMask) | (buffer[dstStartWord] & ~preserveMask);
                }
                if (endOffset != 32)
                {
                    uint preserveMask = uint.MaxValue << endOffset;
                    buffer[dstEndWord] = (savedLast & preserveMask) | (buffer[dstEndWord] & ~preserveMask);
                }
            }
            return;
        }

        // General case: non-word-aligned delta, use barrel shift
        int dstEndBit = dstStartBit + bitsToMove;
        int dstStartWordGen = dstStartBit >> 5;
        int dstEndWordGen = (dstEndBit - 1) >> 5;
        int firstWordOffset = dstStartBit & 31;
        int lastWordEndBit = ((dstEndBit - 1) & 31) + 1;

        // Fast path 3: single word destination - just one barrel shift with mask
        if (dstStartWordGen == dstEndWordGen)
        {
            int srcWordHigh = dstStartWordGen - wordShift;
            int srcWordLow = srcWordHigh - 1;
            uint highBits = (srcWordHigh >= 0 && srcWordHigh < buffer.Length) ? buffer[srcWordHigh] : 0;
            uint lowBits = (srcWordLow >= 0 && srcWordLow < buffer.Length) ? buffer[srcWordLow] : 0;

            ulong combined = ((ulong)highBits << 32) | lowBits;
            uint shifted = (uint)(combined >> (32 - rot));

            uint writeMask = ((1u << (lastWordEndBit - firstWordOffset)) - 1) << firstWordOffset;
            buffer[dstStartWordGen] = (buffer[dstStartWordGen] & ~writeMask) | (shifted & writeMask);
            return;
        }

        // Multi-word case: pre-save boundary words for restoration after loop
        uint savedFirstWord = buffer[dstStartWordGen];
        uint savedLastWord = buffer[dstEndWordGen];

        // Pre-load first highBits (will become lowBits after first iteration)
        int firstSrcWordHigh = dstEndWordGen - wordShift;
        uint highBitsLoop = (firstSrcWordHigh >= 0 && firstSrcWordHigh < buffer.Length) ? buffer[firstSrcWordHigh] : 0;

        // Process backwards to avoid overwriting source - tight loop, fix boundaries after
        for (int dstWord = dstEndWordGen; dstWord >= dstStartWordGen; dstWord--)
        {
            int srcWordLow = dstWord - wordShift - 1;
            uint lowBits = (srcWordLow >= 0 && srcWordLow < buffer.Length) ? buffer[srcWordLow] : 0;

            ulong combined = ((ulong)highBitsLoop << 32) | lowBits;
            buffer[dstWord] = (uint)(combined >> (32 - rot));

            highBitsLoop = lowBits;
        }

        // Fix up boundary words - restore bits outside the write range
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

    /// <summary>
    /// SIMD-optimized shift right for large multi-word shifts.
    /// Uses AVX-512 (16 words), AVX2 (8 words), or SSE2 (4 words) depending on hardware support.
    /// Call this instead of ShiftBitsRight when bitsToMove is large (e.g., >= 256 bits / 8 words).
    /// </summary>
    public static void ShiftBitsRightSimd(Span<uint> buffer, int fromBitPos, int bitsToMove, int delta)
    {
#pragma warning disable CA1857 // Shift operand can't be a constant here 
        if (bitsToMove <= 0 || delta <= 0)
            return;

        int dstStartBit = fromBitPos + delta;
        int rot = delta & 31;
        int wordShift = delta >> 5;

        int dstEndBit = dstStartBit + bitsToMove;
        int dstStartWord = dstStartBit >> 5;
        int dstEndWord = (dstEndBit - 1) >> 5;
        int firstWordOffset = dstStartBit & 31;
        int lastWordEndBit = ((dstEndBit - 1) & 31) + 1;

        uint savedFirstWord = buffer[dstStartWord];
        uint savedLastWord = buffer[dstEndWord];

        int rightShift = 32 - rot;
        int dstWord = dstEndWord;

        // Pre-load high bits for the chain
        int firstSrcWordHigh = dstEndWord - wordShift;
        uint highBitsLoop = (firstSrcWordHigh >= 0 && firstSrcWordHigh < buffer.Length) ? buffer[firstSrcWordHigh] : 0;

        // AVX-512 path: process 16 words at a time
        if (Avx512F.IsSupported && dstWord - 15 >= dstStartWord)
        {
            var blendMask = Vector512.Create(0u, 0u, ~0u, ~0u, 0u, 0u, ~0u, ~0u, 0u, 0u, ~0u, ~0u, 0u, 0u, ~0u, ~0u);
            while (dstWord - 15 >= dstStartWord)
            {
                int srcBase = dstWord - wordShift;
                int srcLow = srcBase - 16;

                // Bounds check for all source words needed
                if (srcLow < 0 || srcBase >= buffer.Length)
                    break;

                // Load source words for 16 destination words
                var high = Vector512.LoadUnsafe(ref buffer[srcBase - 15]);
                var low = Vector512.LoadUnsafe(ref buffer[srcBase - 16]);

                // Interleave to form 64-bit (high << 32) | low pairs
                // UnpackLow/High operate on 128-bit lanes (4 lanes in 512-bit)
                var interleaved02 = Avx512F.UnpackLow(low, high);   // results 0,1 | 4,5 | 8,9 | 12,13
                var interleaved13 = Avx512F.UnpackHigh(low, high);  // results 2,3 | 6,7 | 10,11 | 14,15

                // Shift right by (32 - rot) to get barrel shift result
                var shifted02 = Avx512F.ShiftRightLogical(interleaved02.AsUInt64(), (byte)rightShift);
                var shifted13 = Avx512F.ShiftRightLogical(interleaved13.AsUInt64(), (byte)rightShift);

                // Extract lower 32 bits of each 64-bit value using shuffle (per 128-bit lane)
                var s02 = Avx512F.Shuffle(shifted02.AsUInt32(), 0b00_00_10_00);  // [r0,r1,*,*] per lane
                var s13 = Avx512F.Shuffle(shifted13.AsUInt32(), 0b10_00_00_00);  // [*,*,r2,r3] per lane

                // Blend to get final order - mask 0b1100110011001100 picks positions 2,3,6,7,10,11,14,15 from s13
                var blended = Vector512.ConditionalSelect(blendMask, s13, s02);

                // Store to [dstWord-15, dstWord-14, ..., dstWord]
                blended.StoreUnsafe(ref buffer[dstWord - 15]);

                // Update for next iteration
                highBitsLoop = buffer[srcBase - 16];
                dstWord -= 16;
            }
        }

        // AVX2 path: process 8 words at a time
        if (Avx2.IsSupported && dstWord - 7 >= dstStartWord)
        {
            var blendMask = Vector256.Create(0u, 0u, ~0u, ~0u, 0u, 0u, ~0u, ~0u);
            while (dstWord - 7 >= dstStartWord)
            {
                int srcBase = dstWord - wordShift;
                int srcLow = srcBase - 8;

                // Bounds check for all source words needed
                if (srcLow < 0 || srcBase >= buffer.Length)
                    break;

                // Load source words for 8 destination words
                // high vector: [s-7, s-6, s-5, s-4, s-3, s-2, s-1, s] from buffer[srcBase-7..srcBase]
                // low vector:  [s-8, s-7, s-6, s-5, s-4, s-3, s-2, s-1] from buffer[srcBase-8..srcBase-1]
                var high = Vector256.LoadUnsafe(ref buffer[srcBase - 7]);
                var low = Vector256.LoadUnsafe(ref buffer[srcBase - 8]);

                // Interleave to form 64-bit (high << 32) | low pairs
                // UnpackLow operates on 128-bit lanes: low lane [l0,h0,l1,h1], high lane [l4,h4,l5,h5]
                // UnpackHigh operates on 128-bit lanes: low lane [l2,h2,l3,h3], high lane [l6,h6,l7,h7]
                var interleaved02 = Avx2.UnpackLow(low, high);   // [l0,h0,l1,h1 | l4,h4,l5,h5]
                var interleaved13 = Avx2.UnpackHigh(low, high);  // [l2,h2,l3,h3 | l6,h6,l7,h7]

                // Shift right by (32 - rot) to get barrel shift result
                var shifted02 = Avx2.ShiftRightLogical(interleaved02.AsUInt64(), (byte)rightShift);
                var shifted13 = Avx2.ShiftRightLogical(interleaved13.AsUInt64(), (byte)rightShift);

                // Extract lower 32 bits of each 64-bit value using shuffle
                var s02 = Avx2.Shuffle(shifted02.AsUInt32(), 0b00_00_10_00);  // [r0,r1,*,* | r4,r5,*,*]
                var s13 = Avx2.Shuffle(shifted13.AsUInt32(), 0b10_00_00_00);  // [*,*,r2,r3 | *,*,r6,r7]

                // Blend to get final order [r0,r1,r2,r3,r4,r5,r6,r7]
                var blended = Vector256.ConditionalSelect(blendMask, s13, s02);

                // Store to [dstWord-7, dstWord-6, ..., dstWord]
                blended.StoreUnsafe(ref buffer[dstWord - 7]);

                // Update for next iteration
                highBitsLoop = buffer[srcBase - 8];
                dstWord -= 8;
            }
        }

        // SSE2 path: process 4 words at a time
        if (Sse2.IsSupported && dstWord - 3 >= dstStartWord)
        {
            while (dstWord - 3 >= dstStartWord)
            {
                int srcBase = dstWord - wordShift;
                int srcLow = srcBase - 4;

                // Bounds check for all source words needed
                if (srcLow < 0 || srcBase >= buffer.Length)
                    break;

                // Load source words for 4 destination words
                // high vector: [s-3, s-2, s-1, s] from buffer[srcBase-3..srcBase]
                // low vector:  [s-4, s-3, s-2, s-1] from buffer[srcBase-4..srcBase-1]
                var high = Vector128.LoadUnsafe(ref buffer[srcBase - 3]);
                var low = Vector128.LoadUnsafe(ref buffer[srcBase - 4]);

                // Interleave to form 64-bit (high << 32) | low pairs
                // UnpackLow(low, high) = [l0, h0, l1, h1] as dwords
                //   = [(h0<<32)|l0, (h1<<32)|l1] as qwords (little endian)
                // UnpackHigh(low, high) = [l2, h2, l3, h3] as dwords
                //   = [(h2<<32)|l2, (h3<<32)|l3] as qwords
                var interleaved01 = Sse2.UnpackLow(low, high);
                var interleaved23 = Sse2.UnpackHigh(low, high);

                // Shift right by (32 - rot) to get barrel shift result
                var shifted01 = Sse2.ShiftRightLogical(interleaved01.AsUInt64(), (byte)rightShift);
                var shifted23 = Sse2.ShiftRightLogical(interleaved23.AsUInt64(), (byte)rightShift);

                // Extract lower 32 bits of each 64-bit value
                // Shuffle to get [result0, result1, ?, ?] and [result2, result3, ?, ?]
                var s01 = Sse2.Shuffle(shifted01.AsUInt32(), 0b00_00_10_00).AsUInt64();
                var s23 = Sse2.Shuffle(shifted23.AsUInt32(), 0b00_00_10_00).AsUInt64();

                // Combine low 64-bit halves: [r0,r1] and [r2,r3] -> [r0,r1,r2,r3]
                var result = Sse2.UnpackLow(s01, s23).AsUInt32();

                // Store to [dstWord-3, dstWord-2, dstWord-1, dstWord]
                result.StoreUnsafe(ref buffer[dstWord - 3]);

                // Update for next iteration
                highBitsLoop = buffer[srcBase - 4];
                dstWord -= 4;
            }
        }

        // Scalar fallback for remaining words
        while (dstWord >= dstStartWord)
        {
            int srcWordLow = dstWord - wordShift - 1;
            uint lowBits = (srcWordLow >= 0 && srcWordLow < buffer.Length) ? buffer[srcWordLow] : 0;

            ulong combined = ((ulong)highBitsLoop << 32) | lowBits;
            buffer[dstWord] = (uint)(combined >> rightShift);

            highBitsLoop = lowBits;
            dstWord--;
        }

        // Fix up boundary words
        if (firstWordOffset != 0)
        {
            uint preserveMask = (1u << firstWordOffset) - 1;
            buffer[dstStartWord] = (savedFirstWord & preserveMask) | (buffer[dstStartWord] & ~preserveMask);
        }
        if (lastWordEndBit != 32)
        {
            uint preserveMask = uint.MaxValue << lastWordEndBit;
            buffer[dstEndWord] = (savedLastWord & preserveMask) | (buffer[dstEndWord] & ~preserveMask);
        }
    }


    /// <summary>
    /// SIMD-optimized shift right for large multi-word shifts.
    /// Uses SSE2 to process 4 destination words at a time.
    /// Call this instead of ShiftBitsRight when bitsToMove is large (e.g., >= 256 bits / 8 words).
    /// </summary>
    public static void ShiftBitsRightSimd_nothreshold(Span<uint> buffer, int fromBitPos, int bitsToMove, int delta)
    {
        if (bitsToMove <= 0 || delta <= 0)
            return;

        int dstStartBit = fromBitPos + delta;
        int rot = delta & 31;
        int wordShift = delta >> 5;

        int dstEndBit = dstStartBit + bitsToMove;
        int dstStartWord = dstStartBit >> 5;
        int dstEndWord = (dstEndBit - 1) >> 5;
        int firstWordOffset = dstStartBit & 31;
        int lastWordEndBit = ((dstEndBit - 1) & 31) + 1;

        uint savedFirstWord = buffer[dstStartWord];
        uint savedLastWord = buffer[dstEndWord];

        int rightShift = 32 - rot;
        int dstWord = dstEndWord;

        // Pre-load high bits for the chain
        int firstSrcWordHigh = dstEndWord - wordShift;
        uint highBitsLoop = (firstSrcWordHigh >= 0 && firstSrcWordHigh < buffer.Length) ? buffer[firstSrcWordHigh] : 0;

        // SSE2 path: process 4 words at a time
        if (Sse2.IsSupported)
        {
            while (dstWord - 3 >= dstStartWord)
            {
                int srcBase = dstWord - wordShift;
                int srcLow = srcBase - 4;

                // Bounds check for all source words needed
                if (srcLow < 0 || srcBase >= buffer.Length)
                    break;

                // Load source words for 4 destination words
                // high vector: [s-3, s-2, s-1, s] from buffer[srcBase-3..srcBase]
                // low vector:  [s-4, s-3, s-2, s-1] from buffer[srcBase-4..srcBase-1]
                var high = Vector128.LoadUnsafe(ref buffer[srcBase - 3]);
                var low = Vector128.LoadUnsafe(ref buffer[srcBase - 4]);

                // Interleave to form 64-bit (high << 32) | low pairs
                // UnpackLow(low, high) = [l0, h0, l1, h1] as dwords
                //   = [(h0<<32)|l0, (h1<<32)|l1] as qwords (little endian)
                // UnpackHigh(low, high) = [l2, h2, l3, h3] as dwords
                //   = [(h2<<32)|l2, (h3<<32)|l3] as qwords
                var interleaved01 = Sse2.UnpackLow(low, high);
                var interleaved23 = Sse2.UnpackHigh(low, high);

                // Shift right by (32 - rot) to get barrel shift result
                var shifted01 = Sse2.ShiftRightLogical(interleaved01.AsUInt64(), (byte)rightShift);
                var shifted23 = Sse2.ShiftRightLogical(interleaved23.AsUInt64(), (byte)rightShift);

                // Extract lower 32 bits of each 64-bit value
                // Shuffle to get [result0, result1, ?, ?] and [result2, result3, ?, ?]
                var s01 = Sse2.Shuffle(shifted01.AsInt32(), 0b00_00_10_00);
                var s23 = Sse2.Shuffle(shifted23.AsInt32(), 0b00_00_10_00);

                // Combine low halves: [result0, result1, result2, result3]
                var result = Sse.MoveLowToHigh(s01.AsSingle(), s23.AsSingle()).AsUInt32();

                // Store to [dstWord-3, dstWord-2, dstWord-1, dstWord]
                result.StoreUnsafe(ref buffer[dstWord - 3]);

                // Update for next iteration
                highBitsLoop = buffer[srcBase - 4];
                dstWord -= 4;
            }
        }

        // Scalar fallback for remaining words
        while (dstWord >= dstStartWord)
        {
            int srcWordLow = dstWord - wordShift - 1;
            uint lowBits = (srcWordLow >= 0 && srcWordLow < buffer.Length) ? buffer[srcWordLow] : 0;

            ulong combined = ((ulong)highBitsLoop << 32) | lowBits;
            buffer[dstWord] = (uint)(combined >> rightShift);

            highBitsLoop = lowBits;
            dstWord--;
        }

        // Fix up boundary words
        if (firstWordOffset != 0)
        {
            uint preserveMask = (1u << firstWordOffset) - 1;
            buffer[dstStartWord] = (savedFirstWord & preserveMask) | (buffer[dstStartWord] & ~preserveMask);
        }
        if (lastWordEndBit != 32)
        {
            uint preserveMask = uint.MaxValue << lastWordEndBit;
            buffer[dstEndWord] = (savedLastWord & preserveMask) | (buffer[dstEndWord] & ~preserveMask);
        }
    }
}
