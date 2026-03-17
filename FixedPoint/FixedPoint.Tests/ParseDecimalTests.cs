namespace FixedPoint.Tests;

[TestClass]
public sealed class ParseDecimalTests
{
    // Round-trip helper: Fixed → decimal string → Fixed → decimal string
    static string RoundTrip(Fixed f) => Fixed.ParseDecimalExact(f.ToDecimalString()).ToDecimalString();

    [TestMethod]
    public void RoundTrip_Zero() =>
        Assert.AreEqual(Fixed.Zero.ToDecimalString(), RoundTrip(Fixed.Zero));

    [TestMethod]
    public void RoundTrip_PositiveInteger() =>
        Assert.AreEqual(new Fixed(7).ToDecimalString(), RoundTrip(new Fixed(7)));

    [TestMethod]
    public void RoundTrip_NegativeInteger() =>
        Assert.AreEqual(new Fixed(-3).ToDecimalString(), RoundTrip(new Fixed(-3)));

    [TestMethod]
    public void RoundTrip_Half()
    {
        var half = Fixed.ParseHexStringExact("0.80000000000000000000000000000000");
        Assert.AreEqual(half.ToDecimalString(), RoundTrip(half));
    }

    [TestMethod]
    public void RoundTrip_SmallPi()
    {
        var x = new Fixed(Math.PI / Math.Pow(10, 30));
        Assert.AreEqual(x.ToDecimalString(), RoundTrip(x));
    }

    [TestMethod]
    public void RoundTrip_AllFractionBitsSet()
    {
        // All 128 fraction bits = 1: the largest possible fractional value (just under 1.0)
        var x = Fixed.ParseHexStringExact("0.FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF");
        Assert.AreEqual(x.ToDecimalString(), RoundTrip(x));
    }

    [TestMethod]
    public void RoundTrip_LsbOnly()
    {
        // Smallest non-zero fraction: 2^-128
        var x = Fixed.ParseHexStringExact("0.00000000000000000000000000000001");
        Assert.AreEqual(x.ToDecimalString(), RoundTrip(x));
    }

    [TestMethod]
    public void RoundTrip_NegativeFraction()
    {
        var x = Fixed.ParseHexStringExact("FFFFFFFF.80000000000000000000000000000000"); // -0.5
        Assert.AreEqual(x.ToDecimalString(), RoundTrip(x));
    }

    [TestMethod]
    public void RoundTrip_NegativeOneAndHalf()
    {
        var x = Fixed.ParseHexStringExact("FFFFFFFE.80000000000000000000000000000000"); // -1.5
        Assert.AreEqual(x.ToDecimalString(), RoundTrip(x));
    }

    [TestMethod]
    public void ParseDecimalExact_KnownHalf()
    {
        // ParseDecimalExact("0.5...0") should give the same Fixed as ParseHexStringExact("0.8000...")
        var fromDecimal = Fixed.ParseDecimalExact("0.500000000000000000000000000000000000000");
        var fromHex = Fixed.ParseHexStringExact("0.80000000000000000000000000000000");
        Assert.AreEqual(fromHex.ToHexString(), fromDecimal.ToHexString());
    }

    [TestMethod]
    public void ParseDecimalExact_KnownQuarter()
    {
        var fromDecimal = Fixed.ParseDecimalExact("0.250000000000000000000000000000000000000");
        var fromHex = Fixed.ParseHexStringExact("0.40000000000000000000000000000000");
        Assert.AreEqual(fromHex.ToHexString(), fromDecimal.ToHexString());
    }

    [TestMethod]
    public void ParseDecimalExact_WrongFractionalLength_Throws()
    {
        Assert.ThrowsExactly<FormatException>(() =>
            Fixed.ParseDecimalExact("0.50000000000000000000000000000000000000")); // 38 digits
        Assert.ThrowsExactly<FormatException>(() =>
            Fixed.ParseDecimalExact("0.5000000000000000000000000000000000000000")); // 40 digits
    }
}
