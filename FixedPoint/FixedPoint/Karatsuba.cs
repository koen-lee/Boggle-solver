using System.Buffers;

namespace FixedPoint;

/// <summary>
/// Karatsuba multiplication for fixed-point word arrays.
/// Precondition: all inputs have power-of-two word counts.
/// </summary>
internal static class Karatsuba
{
    // Use schoolbook below this word count.
    private const int SchoolbookThreshold = 4;

    /// <summary>
    /// Writes the high n words of a·b into result, where n = a.Length = b.Length = result.Length.
    /// </summary>
    public static void MultiplyHigh(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b, Span<uint> result)
    {
        int n = a.Length;
        uint[] buf = ArrayPool<uint>.Shared.Rent(2 * n);
        try
        {
            var full = buf.AsSpan(0, 2 * n);
            full.Clear();
            FullMultiply(a, b, full);
            full.Slice(n - 1, n).CopyTo(result);
        }
        finally
        {
            ArrayPool<uint>.Shared.Return(buf);
        }

        if (n == 32 && OnMismatch32 != null)
        {
            var sb = new uint[n];
            SchoolbookMultiplyHigh(a, b, sb);
            if (!result.SequenceEqual(sb))
            {
                var handler = OnMismatch32;
                OnMismatch32 = null; // fire once
                handler(a.ToArray(), b.ToArray(), result.ToArray(), sb);
            }
        }
    }

    /// <summary>
    /// Same contract as MultiplyHigh but forces the schoolbook path regardless of n.
    /// Used to validate Karatsuba correctness by bit-for-bit comparison.
    /// </summary>
    public static void SchoolbookMultiplyHigh(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b, Span<uint> result)
    {
        int n = a.Length;
        uint[] buf = ArrayPool<uint>.Shared.Rent(2 * n);
        try
        {
            var full = buf.AsSpan(0, 2 * n);
            SchoolbookFull(a, b, full);
            full.Slice(n - 1, n).CopyTo(result);
        }
        finally
        {
            ArrayPool<uint>.Shared.Return(buf);
        }
    }

    /// When non-null, called the first time a MultiplyHigh(n=32) result disagrees with schoolbook.
    /// Receives (a, b, karatsuba_result, schoolbook_result), all length 32.
    public static Action<uint[], uint[], uint[], uint[]>? OnMismatch32;

    /// <summary>
    /// Writes the full 2n-word product of a·b into out2n.
    /// Precondition: n = a.Length is a power of two; out2n.Length == 2*n.
    /// </summary>
    private static void FullMultiply(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b, Span<uint> out2n)
    {
        int n = a.Length;
        if (n <= SchoolbookThreshold)
        {
            SchoolbookFull(a, b, out2n);
            return;
        }

        int half = n / 2; // exact: n is always a power of two

        var a_lo = a[..half];
        var a_hi = a[half..];
        var b_lo = b[..half];
        var b_hi = b[half..];

        // Rent all temporary buffers up front.
        uint[] z0buf    = ArrayPool<uint>.Shared.Rent(n);       // 2*half = n words
        uint[] z2buf    = ArrayPool<uint>.Shared.Rent(n);       // n words
        uint[] midAbuf  = ArrayPool<uint>.Shared.Rent(half);
        uint[] midBbuf  = ArrayPool<uint>.Shared.Rent(half);
        uint[] z1buf    = ArrayPool<uint>.Shared.Rent(n + 1);   // n+1: carry correction can overflow n words
        try
        {
            var z0   = z0buf.AsSpan(0, n);
            var z2   = z2buf.AsSpan(0, n);
            var midA = midAbuf.AsSpan(0, half);
            var midB = midBbuf.AsSpan(0, half);
            var z1   = z1buf.AsSpan(0, n + 1);

            z0.Clear(); z2.Clear(); z1.Clear();

            FullMultiply(a_lo, b_lo, z0);
            FullMultiply(a_hi, b_hi, z2);

            uint ca = TruncatedAdd(a_lo, a_hi, midA);
            uint cb = TruncatedAdd(b_lo, b_hi, midB);

            FullMultiply(midA, midB, z1[..n]);

            // Carry corrections: true mid_a = midA + ca*2^(half*32), similarly mid_b.
            // z1_true = midA*midB + ca*(midB<<half) + cb*(midA<<half) + ca*cb*(1<<n)
            // The ca*cb*(1<<n) term falls outside the 2n-word window; ignore it.
            if (ca != 0) AddShifted(z1, midB, half);
            if (cb != 0) AddShifted(z1, midA, half);

            // z1 = z1_prod - z0 - z2  (Karatsuba identity guarantees z1 >= 0)
            SubtractSpans(z1, z0);
            SubtractSpans(z1, z2);

            // Accumulate into out2n: z0 at 0, z1 at half, z2 at n.
            out2n.Clear();
            AddShifted(out2n, z0, 0);
            AddShifted(out2n, z1, half);
            AddShifted(out2n, z2, n);
        }
        finally
        {
            ArrayPool<uint>.Shared.Return(z0buf);
            ArrayPool<uint>.Shared.Return(z2buf);
            ArrayPool<uint>.Shared.Return(midAbuf);
            ArrayPool<uint>.Shared.Return(midBbuf);
            ArrayPool<uint>.Shared.Return(z1buf);
        }
    }

    /// <summary>
    /// Full 2n-word schoolbook product. Used as the base case for n ≤ SchoolbookThreshold.
    /// </summary>
    private static void SchoolbookFull(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b, Span<uint> out2n)
    {
        int n = a.Length;
        out2n.Clear();
        UInt128 carry = 0;
        for (var d = 0; d < 2 * n; d++)
        {
            UInt128 sum = carry;
            for (var i = Math.Max(0, d - (n - 1)); i <= Math.Min(d, n - 1); i++)
                sum += (ulong)a[i] * b[d - i];
            out2n[d] = (uint)sum;
            carry = sum >> 32;
        }
        // carry is 0 for unsigned multiplication within range
    }

    /// <summary>
    /// Adds two equal-length spans word-by-word into result (same length), returns carry-out (0 or 1).
    /// </summary>
    private static uint TruncatedAdd(ReadOnlySpan<uint> x, ReadOnlySpan<uint> y, Span<uint> result)
    {
        ulong carry = 0;
        for (int i = 0; i < x.Length; i++)
        {
            ulong s = (ulong)x[i] + y[i] + carry;
            result[i] = (uint)s;
            carry = s >> 32;
        }
        return (uint)carry;
    }

    /// <summary>
    /// Subtracts sub from target in-place. Guaranteed no final borrow by Karatsuba identity.
    /// </summary>
    private static void SubtractSpans(Span<uint> target, ReadOnlySpan<uint> sub)
    {
        ulong borrow = 0;
        for (int i = 0; i < sub.Length; i++)
        {
            ulong d = (ulong)target[i] - sub[i] - borrow;
            target[i] = (uint)d;
            borrow = d >> 63;
        }
        for (int i = sub.Length; i < target.Length && borrow != 0; i++)
        {
            ulong d = (ulong)target[i] - borrow;
            target[i] = (uint)d;
            borrow = d >> 63;
        }
    }

    /// <summary>
    /// Adds src into target starting at wordOffset, propagating carry.
    /// </summary>
    private static void AddShifted(Span<uint> target, ReadOnlySpan<uint> src, int wordOffset)
    {
        ulong carry = 0;
        int limit = Math.Min(src.Length, target.Length - wordOffset);
        for (int i = 0; i < limit; i++)
        {
            ulong s = (ulong)target[wordOffset + i] + src[i] + carry;
            target[wordOffset + i] = (uint)s;
            carry = s >> 32;
        }
        for (int j = wordOffset + src.Length; j < target.Length && carry != 0; j++)
        {
            ulong s = (ulong)target[j] + carry;
            target[j] = (uint)s;
            carry = s >> 32;
        }
    }
}
