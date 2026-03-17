using System.Diagnostics;
using System.Numerics;

namespace FixedPoint;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public readonly struct Fixed : IComparable<Fixed>, IEquatable<Fixed>
{
    const int Size = 4; // Number of 32-bit fraction words

    // Minimum decimal digits to uniquely represent any Size*32-bit fraction: ceil(Size*32 * log10(2)).
    static readonly int DecimalFracDigits = (int)Math.Ceiling(Size * 32 * Math.Log10(2));

    // LSB-first storage:
    //   _words[0]    = least significant fraction word  (old _fraction[Size-1])
    //   _words[i]    = fraction word i from LSB         (old _fraction[Size-1-i])
    //   _words[Size-1] = most significant fraction word (old _fraction[0])
    //   _words[Size]   = integer part, signed            (old _integer)
    private readonly uint[] _words;

    private int IntegerPart => (int)_words[Size];

    public static readonly Fixed Zero = new(0);

    public Fixed(int value)
    {
        _words = new uint[Size + 1];
        _words[Size] = (uint)value;
    }

    /// <summary>
    /// Converts a double to an exact Fixed-point representation using the double's mantissa bits.
    /// Throws for NaN and infinity.
    /// </summary>
    public Fixed(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new ArgumentOutOfRangeException(nameof(value));

        var bits = BitConverter.DoubleToInt64Bits(value);
        var negative = (bits >> 63) != 0;
        var exponentBits = (int)((bits >> 52) & 0x7FF);
        var mantissa = bits & 0x000FFFFFFFFFFFFFL;

        if (exponentBits == 0 && mantissa == 0)
        {
            _words = new uint[Size + 1];
            return;
        }

        // Normal: implicit leading 1. Subnormal: no leading 1, exponent treated as -1022.
        var M = exponentBits == 0 ? mantissa : mantissa | (1L << 52);
        var shift = exponentBits == 0 ? -1074 : exponentBits - 1075; // exponent - 1023 - 52

        // Place M at _words[Size]=M>>32, _words[Size-1]=M&0xFFFFFFFF → value = M * 2^-32.
        // Then shift by (shift + 32) to reach M * 2^shift.
        var tempWords = new uint[Size + 1];
        tempWords[Size - 1] = (uint)(M & 0xFFFFFFFF); // MSB fraction word
        tempWords[Size]     = (uint)(M >> 32);         // integer part
        var temp = new Fixed(tempWords);

        var totalShift = shift + 32;
        var result = totalShift >= 0 ? temp.ShiftLeft(totalShift) : temp.ShiftRight(-totalShift);
        if (negative) result = result.Negate();

        _words = result._words;
    }

    private Fixed(uint[] words)
    {
        _words = words;
    }

    public static explicit operator Fixed(double value) => new(value);

    public static explicit operator double(Fixed value)
    {
        var result = (double)(int)value._words[Size];
        var factor = 1.0 / 4294967296.0; // 2^-32
        for (var i = Size - 1; i >= 0; i--)
        {
            result += value._words[i] * factor;
            factor /= 4294967296.0;
        }
        return result;
    }

    public Fixed Add(Fixed other)
    {
        var words = new uint[Size + 1];
        ulong carry = 0;
        for (var i = 0; i <= Size; i++)
        {
            var sum = (ulong)_words[i] + other._words[i] + carry;
            words[i] = (uint)sum;
            carry = sum >> 32;
        }
        return new Fixed(words);
    }

    public Fixed Negate()
    {
        var words = new uint[Size + 1];
        ulong carry = 1;
        for (var i = 0; i <= Size; i++)
        {
            var sum = (ulong)(~_words[i]) + carry;
            words[i] = (uint)sum;
            carry = sum >> 32;
        }
        return new Fixed(words);
    }

    public Fixed Subtract(Fixed other)
    {
        var words = new uint[Size + 1];
        ulong borrow = 0;
        for (var i = 0; i <= Size; i++)
        {
            var diff = (ulong)_words[i] - other._words[i] - borrow;
            words[i] = (uint)diff;
            borrow = diff >> 63; // 1 iff the subtraction underflowed
        }
        return new Fixed(words);
    }

    /// <summary>
    /// Sign-extended right shift. Bits shifted out on the right are discarded.
    /// </summary>
    public Fixed ShiftRight(int bits)
    {
        if (bits <= 0) return this;

        var wholeWords = bits / 32;
        var remainder  = bits % 32;
        var signFill   = (uint)((int)_words[Size] >> 31); // 0x00000000 or 0xFFFFFFFF

        // Step 1: shift by whole words.
        uint[] words;
        if (wholeWords > Size)
        {
            var filled = new uint[Size + 1];
            Array.Fill(filled, signFill);
            return new Fixed(filled);
        }

        if (wholeWords == 0)
        {
            words = _words; // reuse directly; step 2 reads from words into a fresh result array
        }
        else
        {
            words = new uint[Size + 1];
            for (var i = 0; i <= Size; i++)
                words[i] = i + wholeWords <= Size ? _words[i + wholeWords] : signFill;
        }

        // Step 2: shift by remaining bits.
        if (remainder == 0)
            return wholeWords == 0 ? this : new Fixed(words);

        var result = new uint[Size + 1];
        for (var i = 0; i < Size; i++)
            result[i] = (words[i] >> remainder) | (words[i + 1] << (32 - remainder));
        result[Size] = (uint)((int)words[Size] >> remainder);
        return new Fixed(result);
    }

    /// <summary>
    /// Left shift. Zeros fill in from the right; bits shifted out on the left are discarded.
    /// </summary>
    public Fixed ShiftLeft(int bits)
    {
        if (bits <= 0) return this;

        var wholeWords = bits / 32;
        var remainder  = bits % 32;

        // Step 1: shift by whole words.
        uint[] words;
        if (wholeWords > Size)
            return Zero;

        if (wholeWords == 0)
        {
            words = _words;
        }
        else
        {
            words = new uint[Size + 1];
            for (var i = 0; i <= Size; i++)
                words[i] = i >= wholeWords ? _words[i - wholeWords] : 0;
        }

        // Step 2: shift by remaining bits.
        if (remainder == 0)
            return wholeWords == 0 ? this : new Fixed(words);

        var result = new uint[Size + 1];
        result[0] = words[0] << remainder;
        for (var i = 1; i <= Size; i++)
            result[i] = (words[i] << remainder) | (words[i - 1] >> (32 - remainder));
        return new Fixed(result);
    }

    /// <summary>
    /// Multiplies this value by a non-negative 32-bit scalar. O(N) — used for decimal digit extraction.
    /// </summary>
    public Fixed MultiplyByUInt(uint factor)
    {
        var words = new uint[Size + 1];
        ulong carry = 0;
        for (var i = 0; i <= Size; i++)
        {
            var val = (ulong)_words[i] * factor + carry;
            words[i] = (uint)val;
            carry = val >> 32;
        }
        return new Fixed(words);
    }

    /// <summary>
    /// Multiplies two Fixed values using a diagonal sweep over the N×N partial-product grid.
    /// Iterates diagonals d = i+j from 0 to 2*Size. Diagonals below Size are outside the
    /// result window but their carry propagates upward. Diagonals Size..2*Size produce the
    /// N result words. UInt128 accumulates up to N products per diagonal without overflow.
    /// </summary>
    public Fixed Multiply(Fixed other)
    {
        var negative = (IntegerPart < 0) != (other.IntegerPart < 0);
        var a = IntegerPart < 0 ? Negate() : this;
        var b = other.IntegerPart < 0 ? other.Negate() : other;

        const int N = Size + 1;
        var words = new uint[N];
        UInt128 carry = 0;
        for (var d = 0; d <= 2 * Size; d++)
        {
            UInt128 sum = carry;
            for (var i = Math.Max(0, d - Size); i <= Math.Min(d, Size); i++)
                sum += (ulong)a._words[i] * b._words[d - i];
            if (d >= Size)
                words[d - Size] = (uint)sum;
            carry = sum >> 32;
        }

        var result = new Fixed(words);
        return negative ? result.Negate() : result;
    }

    /// <summary>
    /// Divides this value by <paramref name="other"/> using Newton-Raphson reciprocal iteration.
    /// Seeds the reciprocal estimate from a double, then refines with x = x*(2 - b*x) three times
    /// (each iteration doubles correct bits; three iterations exceeds 160-bit precision).
    /// </summary>
    public Fixed Divide(Fixed other)
    {
        if (other.Equals(Zero))
            throw new DivideByZeroException();

        var negative = (IntegerPart < 0) != (other.IntegerPart < 0);
        var a = IntegerPart < 0 ? Negate() : this;
        var b = other.IntegerPart < 0 ? other.Negate() : other;

        // Seed: ~52 bits of precision from double reciprocal.
        var x = (Fixed)(1.0 / (double)b);
        var two = new Fixed(2);

        // x = x * (2 - b*x): 3 iterations gives 52 * 2^3 = 416 bits — more than enough.
        for (var i = 0; i < 3; i++)
            x = x.Multiply(two.Subtract(b.Multiply(x)));

        var result = a.Multiply(x);
        return negative ? result.Negate() : result;
    }

    public static Fixed ParseHexStringExact(string s)
    {
        var dot = s.IndexOf('.');
        var fracStr = s[(dot + 1)..];
        var words = new uint[Size + 1];
        words[Size] = Convert.ToUInt32(s[..dot], 16);
        for (var i = 0; i < Size; i++)
            words[Size - 1 - i] = Convert.ToUInt32(fracStr.Substring(i * 8, 8), 16);
        return new Fixed(words);
    }

    /// <summary>
    /// Parses a decimal string produced by ToDecimalString(), exactly recovering the original Fixed value.
    /// F* = floor(D × 2^128 / 10^39) where D is the 39-digit fractional integer.
    /// BigInteger handles the exact arithmetic; no precision is lost.
    /// </summary>
    public static Fixed ParseDecimalExact(string s)
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
        // The inverse ceiling(D × 2^(Size*32) / 10^DecimalFracDigits) = F* exactly, because
        // 10^DecimalFracDigits / 2^(Size*32) > 1 guarantees each F* maps to a unique D.
        var scale = BigInteger.Pow(10, DecimalFracDigits);
        var D = BigInteger.Parse(fracSpan);
        var fracBits = (D * (BigInteger.One << 128) + scale - 1) / scale;

        var words = new uint[Size + 1];
        words[Size] = (uint)intPart;
        for (var i = 0; i < Size; i++)
        {
            words[i] = (uint)(fracBits & 0xFFFFFFFF);
            fracBits >>= 32;
        }

        var result = new Fixed(words);
        return negative ? result.Negate() : result;
    }

    private string GetDebuggerDisplay() => ToHexString();

    public string ToHexString()
    {
        var w = _words;
        var fractionString = string.Join("",
            Enumerable.Range(0, Size).Select(i => w[Size - 1 - i].ToString("X8")));
        return $"{IntegerPart:X}.{fractionString}";
    }

    /// <summary>
    /// Returns the value as a decimal string with exactly DecimalFracDigits fractional digits (trailing zeros kept).
    /// DecimalFracDigits = ceil(Size*32 * log10(2)) — the minimum to uniquely represent all fractions.
    /// Batches of 9 digits are extracted by multiplying the fraction words by 10^9 at a time,
    /// keeping each step to a single O(N) scalar multiply rather than a full O(N^2) Fixed multiply.
    /// </summary>
    public string ToDecimalString()
    {
        if (IntegerPart < 0)
            return "-" + Negate().ToDecimalString();

        const uint BatchBase = 1_000_000_000; // 10^9 < 2^30, so (uint)*BatchBase fits in ulong

        // Work directly on the fraction words; the integer word does not participate.
        var frac = new uint[Size];
        for (var i = 0; i < Size; i++)
            frac[i] = _words[i];

        // 5 batches × 9 digits = 45; we keep the first 39.
        var batchCount = (DecimalFracDigits + 8) / 9; // ceil(DecimalFracDigits / 9)
        var fracChars = new char[batchCount * 9];
        for (var g = 0; g < batchCount; g++)
        {
            ulong carry = 0;
            for (var i = 0; i < Size; i++) // LSW → MSW
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

    public int CompareTo(Fixed other)
    {
        var intComparison = IntegerPart.CompareTo(other.IntegerPart);
        if (intComparison != 0)
            return intComparison;
        for (var i = Size - 1; i >= 0; i--)
        {
            var fracComparison = _words[i].CompareTo(other._words[i]);
            if (fracComparison != 0)
                return fracComparison;
        }
        return 0;
    }

    public bool Equals(Fixed other) => CompareTo(other) == 0;

    public override bool Equals(object? obj) => obj is Fixed other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            foreach (var w in _words)
                hash = hash * 31 + w.GetHashCode();
            return hash;
        }
    }
}
