namespace FixedPoint.Tests;

[TestClass]
public sealed class AddTests
{
    [TestMethod]
    public void Add_TwoPositiveIntegers()
    {
        var result = new Fixed(3).Add(new Fixed(5));
        Assert.AreEqual($"8.{Helpers.ZeroFraction}", result.ToString());
    }

    [TestMethod]
    public void Add_WithZero_ReturnsOriginal()
    {
        var a = new Fixed(7);
        Assert.AreEqual(a.ToString(), a.Add(Fixed.Zero).ToString());
    }

    [TestMethod]
    public void Add_Zero_WithZero_ReturnsZero()
    {
        Assert.AreEqual(Fixed.Zero.ToString(), Fixed.Zero.Add(Fixed.Zero).ToString());
    }

    [TestMethod]
    public void Add_PositiveAndNegative_CancelOut()
    {
        var result = new Fixed(3).Add(new Fixed(-3));
        Assert.AreEqual($"0.{Helpers.ZeroFraction}", result.ToString());
    }

    [TestMethod]
    public void Add_IsCommutative()
    {
        var a = new Fixed(4);
        var b = new Fixed(9);
        Assert.AreEqual(a.Add(b).ToString(), b.Add(a).ToString());
    }

    [TestMethod]
    public void AddThenSubtract_ReturnsOriginal()
    {
        var a = new Fixed(10);
        var b = new Fixed(3);
        Assert.AreEqual(a.ToString(), a.Add(b).Subtract(b).ToString());
    }
}
