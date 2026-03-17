using System.Diagnostics;

namespace FixedPoint;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public readonly partial struct Fixed<TSize> : IComparable<Fixed<TSize>>, IEquatable<Fixed<TSize>> where TSize : struct, IFixedSize
{
     // Number of 32-bit fraction words

    // Newton-Raphson iterations to reach TSize.Value*32 bits from a 53-bit double seed
    // (each iteration doubles correct bits). +1 safety margin.
    static readonly int ReciprocalIterations = (int)Math.Ceiling(Math.Log2(TSize.Value * 32.0 / 53)) + 1;

    // LSB-first storage:
    //   _words[0]    = least significant fraction word  
    //   _words[i]    = fraction word i from LSB         
    //   _words[TSize.Value-1] = most significant fraction word
    //   _words[TSize.Value]   = integer part, signed            
    private readonly uint[] _words;

    private int IntegerPart => (int)_words[TSize.Value];

    public static readonly Fixed<TSize> Zero = new(0);

    public Fixed(int value)
    {
        _words = new uint[TSize.Value + 1];
        _words[TSize.Value] = (uint)value;
    }

    /// <summary>
    /// Converts a double to an exact Fixed<TSize>-point representation using the double's mantissa bits.
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
            _words = new uint[TSize.Value + 1];
            return;
        }

        // Normal: implicit leading 1. Subnormal: no leading 1, exponent treated as -1022.
        var M = exponentBits == 0 ? mantissa : mantissa | (1L << 52);
        var shift = exponentBits == 0 ? -1074 : exponentBits - 1075; // exponent - 1023 - 52

        // Place M at _words[TSize.Value]=M>>32, _words[TSize.Value-1]=M&0xFFFFFFFF → value = M * 2^-32.
        // Then shift by (shift + 32) to reach M * 2^shift.
        var tempWords = new uint[TSize.Value + 1];
        tempWords[TSize.Value - 1] = (uint)(M & 0xFFFFFFFF); // MSB fraction word
        tempWords[TSize.Value] = (uint)(M >> 32);         // integer part
        var temp = new Fixed<TSize>(tempWords);

        var totalShift = shift + 32;
        var result = totalShift >= 0 ? temp.ShiftLeft(totalShift) : temp.ShiftRight(-totalShift);
        if (negative) result = result.Negate();

        _words = result._words;
    }

    private Fixed(uint[] words)
    {
        _words = words;
    }

    public static explicit operator Fixed<TSize>(double value) => new(value);

    public static explicit operator double(Fixed<TSize> value)
    {
        var result = (double)(int)value._words[TSize.Value];
        var factor = 1.0 / 4294967296.0; // 2^-32
        for (var i = TSize.Value - 1; i >= 0; i--)
        {
            result += value._words[i] * factor;
            factor /= 4294967296.0;
        }
        return result;
    }

    public Fixed<TSize> Add(Fixed<TSize> other)
    {
        var words = new uint[TSize.Value + 1];
        ulong carry = 0;
        for (var i = 0; i <= TSize.Value; i++)
        {
            var sum = (ulong)_words[i] + other._words[i] + carry;
            words[i] = (uint)sum;
            carry = sum >> 32;
        }
        return new Fixed<TSize>(words);
    }

    public Fixed<TSize> Negate()
    {
        var words = new uint[TSize.Value + 1];
        ulong carry = 1;
        for (var i = 0; i <= TSize.Value; i++)
        {
            var sum = (ulong)(~_words[i]) + carry;
            words[i] = (uint)sum;
            carry = sum >> 32;
        }
        return new Fixed<TSize>(words);
    }

    public Fixed<TSize> Subtract(Fixed<TSize> other)
    {
        var words = new uint[TSize.Value + 1];
        ulong borrow = 0;
        for (var i = 0; i <= TSize.Value; i++)
        {
            var diff = (ulong)_words[i] - other._words[i] - borrow;
            words[i] = (uint)diff;
            borrow = diff >> 63; // 1 iff the subtraction underflowed
        }
        return new Fixed<TSize>(words);
    }

    /// <summary>
    /// Sign-extended right shift. Bits shifted out on the right are discarded.
    /// </summary>
    public Fixed<TSize> ShiftRight(int bits)
    {
        if (bits == 0) return this;
        if (bits < 0) return ShiftLeft(-bits);

        var wholeWords = bits / 32;
        var remainder = bits % 32;
        var signFill = (uint)((int)_words[TSize.Value] >> 31); // 0x00000000 or 0xFFFFFFFF

        // Step 1: shift by whole words.
        uint[] words;
        if (wholeWords > TSize.Value)
        {
            var filled = new uint[TSize.Value + 1];
            Array.Fill(filled, signFill);
            return new Fixed<TSize>(filled);
        }

        if (wholeWords == 0)
        {
            words = _words; // reuse directly; step 2 reads from words into a fresh result array
        }
        else
        {
            words = new uint[TSize.Value + 1];
            for (var i = 0; i <= TSize.Value; i++)
                words[i] = i + wholeWords <= TSize.Value ? _words[i + wholeWords] : signFill;
        }

        // Step 2: shift by remaining bits.
        if (remainder == 0)
            return wholeWords == 0 ? this : new Fixed<TSize>(words);

        var result = new uint[TSize.Value + 1];
        for (var i = 0; i < TSize.Value; i++)
            result[i] = (words[i] >> remainder) | (words[i + 1] << (32 - remainder));
        result[TSize.Value] = (uint)((int)words[TSize.Value] >> remainder);
        return new Fixed<TSize>(result);
    }

    /// <summary>
    /// Left shift. Zeros fill in from the right; bits shifted out on the left are discarded.
    /// </summary>
    public Fixed<TSize> ShiftLeft(int bits)
    {
        if (bits == 0) return this;
        if (bits < 0) return ShiftRight(-bits);

        var wholeWords = bits / 32;
        var remainder = bits % 32;

        // Step 1: shift by whole words.
        uint[] words;
        if (wholeWords > TSize.Value)
            return Zero;

        if (wholeWords == 0)
        {
            words = _words;
        }
        else
        {
            words = new uint[TSize.Value + 1];
            for (var i = 0; i <= TSize.Value; i++)
                words[i] = i >= wholeWords ? _words[i - wholeWords] : 0;
        }

        // Step 2: shift by remaining bits.
        if (remainder == 0)
            return wholeWords == 0 ? this : new Fixed<TSize>(words);

        var result = new uint[TSize.Value + 1];
        result[0] = words[0] << remainder;
        for (var i = 1; i <= TSize.Value; i++)
            result[i] = (words[i] << remainder) | (words[i - 1] >> (32 - remainder));
        return new Fixed<TSize>(result);
    }

    /// <summary>
    /// Multiplies this value by a non-negative 32-bit scalar. O(N).
    /// </summary>
    public Fixed<TSize> MultiplyByUInt(uint factor)
    {
        var words = new uint[TSize.Value + 1];
        ulong carry = 0;
        for (var i = 0; i <= TSize.Value; i++)
        {
            var val = (ulong)_words[i] * factor + carry;
            words[i] = (uint)val;
            carry = val >> 32;
        }
        return new Fixed<TSize>(words);
    }

    /// <summary>
    /// Multiplies two Fixed<TSize> values using a diagonal sweep over the N×N partial-product grid.
    /// Iterates diagonals d = i+j from 0 to 2*TSize.Value. Diagonals below TSize.Value are outside the
    /// result window but their carry propagates upward. Diagonals TSize.Value..2*TSize.Value produce the
    /// N result words. UInt128 accumulates up to N products per diagonal without overflow.
    /// </summary>
    public Fixed<TSize> Multiply(Fixed<TSize> other)
    {
        var negative = (IntegerPart < 0) != (other.IntegerPart < 0);
        var a = IntegerPart < 0 ? Negate() : this;
        var b = other.IntegerPart < 0 ? other.Negate() : other;

        var words = new uint[TSize.Value + 1];
        TSize.Multiply(a._words, b._words, words);

        var result = new Fixed<TSize>(words);
        return negative ? result.Negate() : result;
    }

    /// <summary>
    /// Returns 1/this using Newton-Raphson iteration: x = x*(2 - this*x).
    /// Seeds from a double reciprocal (~52 bits); three iterations yield 52*2^3 = 416 bits.
    /// </summary>
    public Fixed<TSize> Reciprocal()
    {
        if (Equals(Zero))
            throw new DivideByZeroException();

        var negative = IntegerPart < 0;
        var b = negative ? Negate() : this; // work with |this|

        // Seed: ~52 bits of precision from double reciprocal.
        var x = (Fixed<TSize>)(1.0 / (double)b);
        var two = new Fixed<TSize>(2);

        // x = x*(2 - b*x): each iteration doubles correct bits.
        for (var i = 0; i < ReciprocalIterations; i++)
            x *= two - b * x;

        return negative ? x.Negate() : x;
    }

    /// <summary>Divides this value by <paramref name="other"/>.</summary>
    public Fixed<TSize> Divide(Fixed<TSize> other)
    {
        if (other.Equals(Zero))
            throw new DivideByZeroException();
        return this * other.Reciprocal();
    }

    private string GetDebuggerDisplay() => ToHexString();

    public string ToHexString()
    {
        var w = _words;
        var fractionString = string.Join("",
            Enumerable.Range(0, TSize.Value).Select(i => w[TSize.Value - 1 - i].ToString("X8")));
        return $"{IntegerPart:X}.{fractionString}";
    }


    public int CompareTo(Fixed<TSize> other)
    {
        var intComparison = IntegerPart.CompareTo(other.IntegerPart);
        if (intComparison != 0)
            return intComparison;
        for (var i = TSize.Value - 1; i >= 0; i--)
        {
            var fracComparison = _words[i].CompareTo(other._words[i]);
            if (fracComparison != 0)
                return fracComparison;
        }
        return 0;
    }

    public bool Equals(Fixed<TSize> other) => CompareTo(other) == 0;

    public override bool Equals(object? obj) => obj is Fixed<TSize> other && Equals(other);

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
