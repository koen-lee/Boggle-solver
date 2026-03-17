namespace FixedPoint;

/// <summary>
/// Marker interface for types that specify the size of a Fixed.
/// Value is the number of 32-bit words used for the fractional part of the fixed-point representation.
/// All Fixed have a 32-bit integer part, so total size in bits is (Value+1)*32.
/// </summary>
public interface IFixedSize
{
    static abstract int Value { get; }

    /// <summary>
    /// Writes the high (Value+1) words of a·b into result.
    /// a, b, and result all have length Value+1. Unsigned arithmetic; caller handles sign.
    /// </summary>
    static abstract void Multiply(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b, Span<uint> result);
}

/// <summary>4*32 = 128 bits of fractional precision.</summary>
public struct Size4 : IFixedSize
{
    public static int Value => 4;

    // Schoolbook inline with N=5 hardcoded; JIT can fully unroll.
    public static void Multiply(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b, Span<uint> result)
    {
        UInt128 carry = 0;
        for (var d = 0; d <= 8; d++)
        {
            UInt128 sum = carry;
            for (var i = Math.Max(0, d - 4); i <= Math.Min(d, 4); i++)
                sum += (ulong)a[i] * b[d - i];
            if (d >= 4) result[d - 4] = (uint)sum;
            carry = sum >> 32;
        }
    }
}

/// <summary>7*32 = 224 bits of fractional precision. Uses Karatsuba (n=8, one split above schoolbook).</summary>
public struct Size7 : IFixedSize
{
    public static int Value => 7;
    public static void Multiply(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b, Span<uint> result)
        => Karatsuba.MultiplyHigh(a, b, result);
}

/// <summary>7*32 bits — same as Size7 but schoolbook. For correctness testing.</summary>
public struct Size7Schoolbook : IFixedSize
{
    public static int Value => 7;
    public static void Multiply(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b, Span<uint> result)
        => Karatsuba.SchoolbookMultiplyHigh(a, b, result);
}

/// <summary>15*32 = 480 bits of fractional precision. Uses Karatsuba (n=16, two splits above schoolbook).</summary>
public struct Size15 : IFixedSize
{
    public static int Value => 15;
    public static void Multiply(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b, Span<uint> result)
        => Karatsuba.MultiplyHigh(a, b, result);
}

/// <summary>15*32 bits — same as Size15 but schoolbook. For correctness testing.</summary>
public struct Size15Schoolbook : IFixedSize
{
    public static int Value => 15;
    public static void Multiply(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b, Span<uint> result)
        => Karatsuba.SchoolbookMultiplyHigh(a, b, result);
}

/// <summary>31*32 bits. Uses Karatsuba (n=32, three splits above schoolbook).</summary>
public struct Size31 : IFixedSize
{
    public static int Value => 31;
    public static void Multiply(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b, Span<uint> result)
        => Karatsuba.MultiplyHigh(a, b, result);
}

public struct Size31Schoolbook : IFixedSize
{
    public static int Value => 31;
    public static void Multiply(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b, Span<uint> result)
        => Karatsuba.SchoolbookMultiplyHigh(a, b, result);
}

/// <summary>63*32 bits. Uses Karatsuba (n=64, four splits above schoolbook).</summary>
public struct Size63 : IFixedSize
{
    public static int Value => 63;
    public static void Multiply(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b, Span<uint> result)
        => Karatsuba.MultiplyHigh(a, b, result);
}

public struct Size63Schoolbook : IFixedSize
{
    public static int Value => 63;
    public static void Multiply(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b, Span<uint> result)
        => Karatsuba.SchoolbookMultiplyHigh(a, b, result);
}

/// <summary>255*32 = 8160 bits of fractional precision.</summary>
public struct Size255 : IFixedSize
{
    public static int Value => 255;
    public static void Multiply(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b, Span<uint> result)
        => Karatsuba.MultiplyHigh(a, b, result);
}

/// <summary>255*32 = 8160 bits — same precision as Size255 but using schoolbook multiply. For correctness testing only.</summary>
public struct Size255Schoolbook : IFixedSize
{
    public static int Value => 255;
    public static void Multiply(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b, Span<uint> result)
        => Karatsuba.SchoolbookMultiplyHigh(a, b, result);
}

/// <summary>1023*32 = 32736 bits of fractional precision.</summary>
public struct Size1023 : IFixedSize
{
    public static int Value => 1023;
    public static void Multiply(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b, Span<uint> result)
        => Karatsuba.MultiplyHigh(a, b, result);
}

/// <summary>8191*32 = 262112 bits of fractional precision.</summary>
public struct Size8191 : IFixedSize
{
    public static int Value => 8191;
    public static void Multiply(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b, Span<uint> result)
        => Karatsuba.MultiplyHigh(a, b, result);
}
