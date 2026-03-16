using System.Diagnostics;

namespace FixedPoint;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public readonly struct Fixed
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
            }
        }

        if (remainder == 0)
            return new Fixed(integer, fraction);

        // Step 2: shift the remainder bits using the existing sub-32 logic.
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

    private string GetDebuggerDisplay()
    {
        return ToString();
    }

    public override string ToString()
    {
        var fractionString = string.Join("", _fraction.Select(f => f.ToString("X8")));
        return $"{_integer:X}.{fractionString}";
    }
}
