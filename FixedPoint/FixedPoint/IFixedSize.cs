namespace FixedPoint;

/// <summary>
/// Marker interface for types that specify the size of a Fixed.
/// Value is the number of 32-bit words used for the fractional part of the fixed-point representation.
/// All Fixed have a 32-bit integer part, so total size in bits is (Value+1)*32.
/// </summary>
public interface IFixedSize
{
    static abstract int Value { get; }
}

/// <summary>
/// 4*32 = 128 bits of fractional precision.
/// </summary>
public struct Size4    : IFixedSize { public static int Value =>    4; }
/// <summary>
/// 255*32 = 8160 bits of fractional precision.
/// </summary>
public struct Size255  : IFixedSize { public static int Value =>  255; }
/// <summary>
/// 1023*32 = 32736 bits of fractional precision.
/// </summary>
public struct Size1023 : IFixedSize { public static int Value => 1023; }
/// <summary>
/// 8191*32 = 262112 bits of fractional precision.
/// </summary>
public struct Size8191 : IFixedSize { public static int Value => 8191; }
