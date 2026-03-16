using System.Diagnostics;

namespace FixedPoint;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public readonly struct Fixed: IComparable<Fixed>, IEquatable<Fixed>
{
    const int Size = 4; // Number of uints in the fraction part

    private readonly int _integer;
    private readonly uint[] _fraction;

    public static readonly Fixed Zero = new(0);

    public Fixed(int value)
    {
        _integer = value;
        _fraction = new uint[Size];
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
            _integer = 0;
            _fraction = new uint[Size];
            return;
        }

        // Normal: implicit leading 1. Subnormal: no leading 1, exponent treated as -1022.
        var M = exponentBits == 0 ? mantissa : mantissa | (1L << 52);
        var shift = exponentBits == 0 ? -1074 : exponentBits - 1075; // exponent - 1023 - 52

        // Place M: integer = M>>32, fraction[0] = M & 0xFFFFFFFF → represents M * 2^-32.
        // Then shift by (shift + 32) to reach M * 2^shift.
        var frac = new uint[Size];
        frac[0] = (uint)(M & 0xFFFFFFFF);
        var temp = new Fixed((int)(M >> 32), frac);

        var totalShift = shift + 32;
        var result = totalShift >= 0 ? temp.ShiftLeft(totalShift) : temp.ShiftRight(-totalShift);
        if (negative) result = result.Negate();

        _integer = result._integer;
        _fraction = result._fraction;
    }

    public static explicit operator Fixed(double value) => new(value);

    public static explicit operator double(Fixed value)
    {
        var result = (double)value._integer;
        var scale = 1.0 / 4294967296.0; // 2^-32
        for (var i = 0; i < Size; i++)
        {
            result += value._fraction[i] * scale;
            scale /= 4294967296.0;
        }
        return result;
    }

    private Fixed(int integer, uint[] fraction)
    {
        _integer = integer;
        _fraction = fraction;
    }

    public Fixed Add(Fixed other)
    {
        var fraction = new uint[Size];
        var carry = 0U;
        for (var i = Size - 1; i >= 0; i--)
        {
            var sum = _fraction[i] + other._fraction[i] + carry;
            fraction[i] = sum & 0xFFFFFFFF;
            carry = sum >> 32;
        }
        var integer = _integer + other._integer + (int)carry;
        return new Fixed(integer, fraction);
    }

    public Fixed Negate()
    {
        var fraction = new uint[Size];
        var carry = 1U;
        for (var i = Size - 1; i >= 0; i--)
        {
            fraction[i] = ~_fraction[i] + carry;
            carry = (fraction[i] == 0) ? 1U : 0U;
        }
        var integer = ~_integer + (int)carry;
        return new Fixed(integer, fraction);
    }

    public Fixed Subtract(Fixed other)
    {
        var fraction = new uint[Size];
        var borrow = 0U;
        for (var i = Size - 1; i >= 0; i--)
        {
            var diff = _fraction[i] - other._fraction[i] - borrow;
            fraction[i] = diff & 0xFFFFFFFF;
            borrow = (diff >> 32) & 1; // Borrow if the result is negative
        }
        var integer = _integer - other._integer - (int)borrow;
        return new Fixed(integer, fraction);
    }

    /// <summary>
    /// Sign-extended right shift by the specified number of bits. Shifts in sign bits from the left; bits shifted out on the right are discarded.
    /// </summary>
    /// <param name="bits">The number of bits to shift.</param>
    /// <returns>A new Fixed instance representing the shifted value.</returns>
    public Fixed ShiftRight(int bits)
    {
        if (bits <= 0) return this;

        var wholeElements = bits / 32;
        var remainder = bits % 32;

        // Step 1: shift by whole 32-bit elements.
        // The integer sign-extends; its raw bits slide into fraction[wholeElements-1];
        // sign bits fill any gap above that; old fraction elements slide down.
        int integer;
        uint[] fraction;

        if (wholeElements == 0)
        {
            integer = _integer;
            fraction = _fraction;
        }
        else
        {
            integer = _integer >> 31; // 0 or -1
            fraction = new uint[Size];
            var intSign = (uint)integer;

            if (wholeElements <= Size)
            {
                for (var i = 0; i < wholeElements - 1; i++)
                    fraction[i] = intSign;
                fraction[wholeElements - 1] = (uint)_integer;
                for (var i = wholeElements; i < Size; i++)
                    fraction[i] = _fraction[i - wholeElements];
            }
            else
            {
                // All original bits shifted out; only sign remains.
                Array.Fill(fraction, intSign);
                return new Fixed(integer, fraction);
            }
        }
        // Step 2: shift the remainder bits using the existing sub-32 logic.
        return ShiftRightFew(remainder, integer, fraction);
    }

    /// <summary>
    /// Left shift by the specified number of bits. Zeros fill in from the right; bits shifted out on the left are discarded (overflow).
    /// </summary>
    public Fixed ShiftLeft(int bits)
    {
        if (bits <= 0) return this;

        var wholeElements = bits / 32;
        var remainder = bits % 32;

        // Step 1: shift by whole 32-bit elements.
        // Fraction elements slide up; the element landing in the integer position is
        // reinterpreted as signed. Vacated fraction slots at the bottom fill with zero.
        int integer;
        uint[] fraction;

        if (wholeElements == 0)
        {
            integer = _integer;
            fraction = _fraction;
        }
        else if (wholeElements <= Size)
        {
            fraction = new uint[Size]; // zero-initialised
            integer = (int)_fraction[wholeElements - 1];
            for (var i = 0; i < Size - wholeElements; i++)
                fraction[i] = _fraction[i + wholeElements];
        }
        else
        {
            // All original bits shifted out.
            return Zero;
        }

        return ShiftLeftFew(remainder, integer, fraction);
    }

    private static Fixed ShiftLeftFew(int remainder, int integer, uint[] fraction)
    {
        if (remainder == 0)
            return new Fixed(integer, fraction);

        var resultFraction = new uint[Size];
        var newInteger = (integer << remainder) | (int)(fraction[0] >> (32 - remainder));
        for (var i = 0; i < Size - 1; i++)
            resultFraction[i] = (fraction[i] << remainder) | (fraction[i + 1] >> (32 - remainder));
        resultFraction[Size - 1] = fraction[Size - 1] << remainder;
        return new Fixed(newInteger, resultFraction);
    }

    private static Fixed ShiftRightFew(int remainder, int integer, uint[] fraction)
    {
        if (remainder == 0)
            return new Fixed(integer, fraction);

        var resultFraction = new uint[Size];
        var carry = (uint)integer << (32 - remainder);
        for (var i = 0; i < Size; i++)
        {
            var current = fraction[i];
            resultFraction[i] = (current >> remainder) | carry;
            carry = current << (32 - remainder);
        }
        return new Fixed(integer >> remainder, resultFraction);
    }

    public static Fixed ParseHexStringExact(string s)
    {
        var dot = s.IndexOf('.');
        var integer = (int)Convert.ToUInt32(s[..dot], 16);
        var fracStr = s[(dot + 1)..];
        var fraction = new uint[Size];
        for (var i = 0; i < Size; i++)
            fraction[i] = Convert.ToUInt32(fracStr.Substring(i * 8, 8), 16);
        return new Fixed(integer, fraction);
    }

    private string GetDebuggerDisplay()
    {
        return ToHexString();
    }

    public string ToHexString()
    {
        var fractionString = string.Join("", _fraction.Select(f => f.ToString("X8")));
        return $"{_integer:X}.{fractionString}";
    }

    public override string ToString()
    {
        if (_integer < 0)
            return "-" + Negate().ToHexString();
        return ToHexString();
    }

    public int CompareTo(Fixed other)
    {
        var intComparison = _integer.CompareTo(other._integer);
        if (intComparison != 0)
            return intComparison;
        for (var i = 0; i < Size; i++)
        {
            var fracComparison = _fraction[i].CompareTo(other._fraction[i]);
            if (fracComparison != 0)
                return fracComparison;
        }
        return 0;
    }

    public bool Equals(Fixed other)
    {
        return CompareTo(other) == 0;
    }

    public override bool Equals(object? obj)
    {
        return obj is Fixed other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + _integer.GetHashCode();
            foreach (var frac in _fraction)
                hash = hash * 31 + frac.GetHashCode();
            return hash;
        }
    }
}
