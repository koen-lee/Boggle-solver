namespace FixedPoint.Tests;

internal static class Helpers
{
    public static int IntegerPart(Fixed f)
    {
        var s = f.ToHexString();
        return (int)Convert.ToUInt32(s[..s.IndexOf('.')], 16);
    }
}
