namespace FixedPoint.Tests;

[TestClass]
public sealed class ConstructorTests
{
    [TestMethod]
    public void Zero_HasZeroIntegerAndFraction()
    {
        Assert.AreEqual("0.00000000000000000000000000000000", Fixed.Zero.ToHexString());
    }

    [TestMethod]
    public void Constructor_PositiveInteger_SetsIntegerPart()
    {
        Assert.AreEqual("5.00000000000000000000000000000000", new Fixed(5).ToHexString());
    }

    [TestMethod]
    public void Constructor_NegativeInteger_ShowsTwosComplement()
    {
        Assert.AreEqual("FFFFFFFF.00000000000000000000000000000000", new Fixed(-1).ToHexString());
    }

    [TestMethod]
    public void Constructor_Zero_MatchesZeroConstant()
    {
        Assert.AreEqual(Fixed.Zero.ToHexString(), new Fixed(0).ToHexString());
    }

    [TestMethod]
    public void ToHexString_LargePositiveInteger_FormatsAsHex()
    {
        Assert.AreEqual("10.00000000000000000000000000000000", new Fixed(16).ToHexString());
    }

    [TestMethod]
    public void ParseHexStringExact_RoundtripsWithToHexString()
    {
        Assert.AreEqual(new Fixed(42).ToHexString(), Fixed.ParseHexStringExact("2A.00000000000000000000000000000000").ToHexString());
    }

    [TestMethod]
    public void ParseHexStringExact_RoundtripsNegativeInteger()
    {
        Assert.AreEqual(new Fixed(-1).ToHexString(), Fixed.ParseHexStringExact("FFFFFFFF.00000000000000000000000000000000").ToHexString());
    }
}
