using System.Numerics;

namespace FixedPoint;

public readonly partial struct Fixed<TSize> where TSize : struct, IFixedSize
{
  
    // Minimum decimal digits to uniquely represent any TSize.Value*32-bit fraction: ceil(TSize.Value*32 * log10(2)).
    static readonly int DecimalFracDigits = (int)Math.Ceiling(TSize.Value * 32 * Math.Log10(2));

    public static Fixed<TSize> ParseHexStringExact(string s)
    {
        var dot = s.IndexOf('.');
        var fracStr = s[(dot + 1)..];
        var words = new uint[TSize.Value + 1];
        words[TSize.Value] = Convert.ToUInt32(s[..dot], 16);
        for (var i = 0; i < TSize.Value; i++)
            words[TSize.Value - 1 - i] = Convert.ToUInt32(fracStr.Substring(i * 8, 8), 16);
        return new Fixed<TSize>(words);
    }

    /// <summary>
    /// Parses a decimal string produced by ToDecimalString(), exactly recovering the original Fixed<TSize> value.
    /// F* = floor(D × 2^128 / 10^39) where D is the 39-digit fractional integer.
    /// BigInteger handles the exact arithmetic; no precision is lost.
    /// </summary>
    public static Fixed<TSize> ParseDecimalExact(string s)
    {
        var span = s.AsSpan();
        var negative = span[0] == '-';
        if (negative) span = span[1..];

        var dot = span.IndexOf('.');
        var intPart = int.Parse(span[..dot]);

        var fracSpan = span[(dot + 1)..];
        if (fracSpan.Length != DecimalFracDigits)
            throw new FormatException($"Expected exactly {DecimalFracDigits} fractional digits, got {fracSpan.Length}.");

        // F* = ceiling(D × 2^128 / 10^DecimalFracDigits).
        // ceiling is required because ToDecimalString uses floor: D = floor(F* × 10^DecimalFracDigits / 2^128).
        // The inverse ceiling(D × 2^(TSize.Value*32) / 10^DecimalFracDigits) = F* exactly, because
        // 10^DecimalFracDigits / 2^(TSize.Value*32) > 1 guarantees each F* maps to a unique D.
        var scale = BigInteger.Pow(10, DecimalFracDigits);
        var D = BigInteger.Parse(fracSpan);
        var fracBits = (D * (BigInteger.One << (TSize.Value * 32)) + scale - 1) / scale;

        var words = new uint[TSize.Value + 1];
        words[TSize.Value] = (uint)intPart;
        for (var i = 0; i < TSize.Value; i++)
        {
            words[i] = (uint)(fracBits & 0xFFFFFFFF);
            fracBits >>= 32;
        }

        var result = new Fixed<TSize>(words);
        return negative ? result.Negate() : result;
    }

    /// <summary>
    /// Returns the value as a decimal string with exactly DecimalFracDigits fractional digits (trailing zeros kept).
    /// DecimalFracDigits = ceil(TSize.Value*32 * log10(2)) — the minimum to uniquely represent all fractions.
    /// Batches of 9 digits are extracted by multiplying the fraction words by 10^9 at a time,
    /// keeping each step to a single O(N) scalar multiply rather than a full O(N^2) Fixed<TSize> multiply.
    /// </summary>
    public string ToDecimalString()
    {
        if (IntegerPart < 0)
            return "-" + Negate().ToDecimalString();

        const uint BatchBase = 1_000_000_000; // 10^9 < 2^30, so (uint)*BatchBase fits in ulong

        // Work directly on the fraction words; the integer word does not participate.
        var frac = new uint[TSize.Value];
        for (var i = 0; i < TSize.Value; i++)
            frac[i] = _words[i];

        // 5 batches × 9 digits = 45; we keep the first 39.
        var batchCount = (DecimalFracDigits + 8) / 9; // ceil(DecimalFracDigits / 9)
        var fracChars = new char[batchCount * 9];
        for (var g = 0; g < batchCount; g++)
        {
            ulong carry = 0;
            for (var i = 0; i < TSize.Value; i++) // LSW → MSW
            {
                var val = (ulong)frac[i] * BatchBase + carry;
                frac[i] = (uint)val;
                carry = val >> 32;
            }
            // carry is 0..999_999_999 — the next 9 decimal digits
            var batch = ((uint)carry).ToString("D9");
            for (var k = 0; k < 9; k++)
                fracChars[g * 9 + k] = batch[k];
        }

        return $"{IntegerPart}.{new string(fracChars, 0, DecimalFracDigits)}";
    }

    public override string ToString()
    {
        if (IntegerPart < 0)
            return "-" + Negate().ToHexString();
        return ToHexString();
    }
}