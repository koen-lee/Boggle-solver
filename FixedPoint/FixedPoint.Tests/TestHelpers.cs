namespace FixedPoint.Tests;

internal static class Helpers
{
    public static string ZeroFraction => new string('0', 32);

    public static int IntegerPart(Fixed f)
    {
        var s = f.ToString();
        var hexPart = s[..s.IndexOf('.')];
        return (int)Convert.ToUInt32(hexPart, 16);
    }
}
