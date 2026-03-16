namespace FixedPoint.Tests;

[TestClass]
public sealed class SubtractTests
{
    [TestMethod]
    public void Subtract_PositiveIntegers()
    {
        Assert.AreEqual("7.00000000000000000000000000000000", new Fixed(10).Subtract(new Fixed(3)).ToHexString());
    }

    [TestMethod]
    public void Subtract_SameValue_ReturnsZero()
    {
        var a = new Fixed(5);
        Assert.AreEqual("0.00000000000000000000000000000000", a.Subtract(a).ToHexString());
    }

    [TestMethod]
    public void Subtract_Zero_ReturnsOriginal()
    {
        var a = new Fixed(6);
        Assert.AreEqual(a.ToHexString(), a.Subtract(Fixed.Zero).ToHexString());
    }

    [TestMethod]
    public void Subtract_ProducingNegativeResult()
    {
        // 2 - 3 = -1 in two's complement
        Assert.AreEqual("FFFFFFFF.00000000000000000000000000000000", new Fixed(2).Subtract(new Fixed(3)).ToHexString());
    }
}
