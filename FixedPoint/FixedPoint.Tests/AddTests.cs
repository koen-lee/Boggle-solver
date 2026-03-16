namespace FixedPoint.Tests;

[TestClass]
public sealed class AddTests
{
    [TestMethod]
    public void Add_TwoPositiveIntegers()
    {
        Assert.AreEqual("8.00000000000000000000000000000000", new Fixed(3).Add(new Fixed(5)).ToHexString());
    }

    [TestMethod]
    public void Add_WithZero_ReturnsOriginal()
    {
        var a = new Fixed(7);
        Assert.AreEqual(a.ToHexString(), a.Add(Fixed.Zero).ToHexString());
    }

    [TestMethod]
    public void Add_Zero_WithZero_ReturnsZero()
    {
        Assert.AreEqual(Fixed.Zero.ToHexString(), Fixed.Zero.Add(Fixed.Zero).ToHexString());
    }

    [TestMethod]
    public void Add_PositiveAndNegative_CancelOut()
    {
        Assert.AreEqual("0.00000000000000000000000000000000", new Fixed(3).Add(new Fixed(-3)).ToHexString());
    }

    [TestMethod]
    public void Add_IsCommutative()
    {
        var a = new Fixed(4);
        var b = new Fixed(9);
        Assert.AreEqual(a.Add(b).ToHexString(), b.Add(a).ToHexString());
    }

    [TestMethod]
    public void AddThenSubtract_ReturnsOriginal()
    {
        var a = new Fixed(10);
        var b = new Fixed(3);
        Assert.AreEqual(a.ToHexString(), a.Add(b).Subtract(b).ToHexString());
    }

    [TestMethod]
    public void Add_FractionCarryPropagatesIntoInteger()
    {
        // 0xFFFFFFFF * 2^-32 + 0x00000001 * 2^-32 = 1.0 exactly.
        var a = Fixed.ParseHexStringExact("0.FFFFFFFF000000000000000000000000");
        var b = Fixed.ParseHexStringExact("0.00000001000000000000000000000000");
        Assert.AreEqual("1.00000000000000000000000000000000", a.Add(b).ToHexString());
    }
}
