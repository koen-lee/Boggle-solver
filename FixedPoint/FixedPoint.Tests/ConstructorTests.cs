namespace FixedPoint.Tests;

[TestClass]
public sealed class ConstructorTests
{
    [TestMethod]
    public void Zero_HasZeroIntegerAndFraction()
    {
        Assert.AreEqual($"0.{Helpers.ZeroFraction}", Fixed.Zero.ToString());
    }

    [TestMethod]
    public void Constructor_PositiveInteger_SetsIntegerPart()
    {
        Assert.AreEqual($"5.{Helpers.ZeroFraction}", new Fixed(5).ToString());
    }

    [TestMethod]
    public void Constructor_NegativeInteger_ShowsTwosComplement()
    {
        // -1 in hex two's complement is FFFFFFFF
        Assert.AreEqual($"FFFFFFFF.{Helpers.ZeroFraction}", new Fixed(-1).ToString());
    }

    [TestMethod]
    public void Constructor_Zero_MatchesZeroConstant()
    {
        Assert.AreEqual(Fixed.Zero.ToString(), new Fixed(0).ToString());
    }

    [TestMethod]
    public void ToString_LargePositiveInteger_FormatsAsHex()
    {
        Assert.AreEqual($"10.{Helpers.ZeroFraction}", new Fixed(16).ToString());
    }
}
