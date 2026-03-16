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
        if (bits >= 32) throw new NotImplementedException("Shifting by more than 32 bits is not implemented.");

        var fraction = new uint[Size];
        var carry = (uint)_integer << (32 - bits);
        for (var i = 0; i < Size; i++)
        {
            var current = _fraction[i];
            fraction[i] = (current >> bits) | carry;
            carry = (current << (32 - bits)) & 0xFFFFFFFF;
        }
        var integer = _integer >> bits;
        return new Fixed(integer, fraction);
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
