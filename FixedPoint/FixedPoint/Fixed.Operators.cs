namespace FixedPoint;

public readonly partial struct Fixed<TSize> where TSize : struct, IFixedSize
{
    public static Fixed<TSize> operator +(Fixed<TSize> a, Fixed<TSize> b) => a.Add(b);
    public static Fixed<TSize> operator -(Fixed<TSize> a, Fixed<TSize> b) => a.Subtract(b);
    public static Fixed<TSize> operator -(Fixed<TSize> a)          => a.Negate();
    public static Fixed<TSize> operator *(Fixed<TSize> a, Fixed<TSize> b) => a.Multiply(b);
    public static Fixed<TSize> operator *(Fixed<TSize> a, uint b)  => a.MultiplyByUInt(b);
    public static Fixed<TSize> operator /(Fixed<TSize> a, Fixed<TSize> b) => a.Divide(b);
    public static Fixed<TSize> operator <<(Fixed<TSize> a, int bits) => a.ShiftLeft(bits);
    public static Fixed<TSize> operator >>(Fixed<TSize> a, int bits) => a.ShiftRight(bits);

    public static bool operator ==(Fixed<TSize> a, Fixed<TSize> b) => a.Equals(b);
    public static bool operator !=(Fixed<TSize> a, Fixed<TSize> b) => !a.Equals(b);
    public static bool operator < (Fixed<TSize> a, Fixed<TSize> b) => a.CompareTo(b) < 0;
    public static bool operator > (Fixed<TSize> a, Fixed<TSize> b) => a.CompareTo(b) > 0;
    public static bool operator <=(Fixed<TSize> a, Fixed<TSize> b) => a.CompareTo(b) <= 0;
    public static bool operator >=(Fixed<TSize> a, Fixed<TSize> b) => a.CompareTo(b) >= 0;
}
