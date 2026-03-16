namespace FixedPoint.Tests;

[TestClass]
public sealed class SubtractTests
{
    [TestMethod]
    public void Subtract_PositiveIntegers()
    {
        var result = new Fixed(10).Subtract(new Fixed(3));
        Assert.AreEqual($"7.{Helpers.ZeroFraction}", result.ToString());
    }

    [TestMethod]
    public void Subtract_SameValue_ReturnsZero()
    {
        var a = new Fixed(5);
        Assert.AreEqual($"0.{Helpers.ZeroFraction}", a.Subtract(a).ToString());
    }

    [TestMethod]
    public void Subtract_Zero_ReturnsOriginal()
    {
        var a = new Fixed(6);
        Assert.AreEqual(a.ToString(), a.Subtract(Fixed.Zero).ToString());
    }

    [TestMethod]
    public void Subtract_ProducingNegativeResult()
    {
        // 2 - 3 = -1
        var result = new Fixed(2).Subtract(new Fixed(3));
        Assert.AreEqual($"FFFFFFFF.{Helpers.ZeroFraction}", result.ToString());
    }
}
