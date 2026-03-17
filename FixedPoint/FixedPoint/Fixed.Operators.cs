namespace FixedPoint;

public readonly partial struct Fixed
{
    public static Fixed operator +(Fixed a, Fixed b) => a.Add(b);
    public static Fixed operator -(Fixed a, Fixed b) => a.Subtract(b);
    public static Fixed operator -(Fixed a)          => a.Negate();
    public static Fixed operator *(Fixed a, Fixed b) => a.Multiply(b);
    public static Fixed operator *(Fixed a, uint b)  => a.MultiplyByUInt(b);
    public static Fixed operator /(Fixed a, Fixed b) => a.Divide(b);
    public static Fixed operator <<(Fixed a, int bits) => a.ShiftLeft(bits);
    public static Fixed operator >>(Fixed a, int bits) => a.ShiftRight(bits);

    public static bool operator ==(Fixed a, Fixed b) => a.Equals(b);
    public static bool operator !=(Fixed a, Fixed b) => !a.Equals(b);
    public static bool operator < (Fixed a, Fixed b) => a.CompareTo(b) < 0;
    public static bool operator > (Fixed a, Fixed b) => a.CompareTo(b) > 0;
    public static bool operator <=(Fixed a, Fixed b) => a.CompareTo(b) <= 0;
    public static bool operator >=(Fixed a, Fixed b) => a.CompareTo(b) >= 0;
}
