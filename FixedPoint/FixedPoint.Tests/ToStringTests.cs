namespace FixedPoint.Tests;

// ToString() shows signed hex: positive values match ToHexString(),
// negative values show "-" followed by the magnitude in hex.

[TestClass]
public sealed class ToStringTests
{
    [TestMethod]
    public void ToString_Zero_IsZero()
    {
        Assert.AreEqual("0.00000000000000000000000000000000", Fixed.Zero.ToString());
    }

    [TestMethod]
    public void ToString_PositiveInteger_SameAsToHexString()
    {
        Assert.AreEqual("5.00000000000000000000000000000000", new Fixed(5).ToString());
    }

    [TestMethod]
    public void ToString_NegativeOne_ShowsSignAndMagnitude()
    {
        Assert.AreEqual("-1.00000000000000000000000000000000", new Fixed(-1).ToString());
    }

    [TestMethod]
    public void ToString_NegativeTwo_ShowsSignAndMagnitude()
    {
        Assert.AreEqual("-2.00000000000000000000000000000000", new Fixed(-2).ToString());
    }

    [TestMethod]
    public void ToString_NegativeHalf_ShowsSignAndMagnitude()
    {
        // -0.5: raw hex FFFFFFFF.80000000..., displayed as -0.80000000...
        var negHalf = new Fixed(-1).ShiftRight(1);
        Assert.AreEqual("-0.80000000000000000000000000000000", negHalf.ToString());
    }

    [TestMethod]
    public void ToString_NegativeOnePointFive_ShowsSignAndMagnitude()
    {
        // -1.5: raw hex FFFFFFFE.80000000..., displayed as -1.80000000...
        var negOnePointFive = new Fixed(-3).ShiftRight(1);
        Assert.AreEqual("-1.80000000000000000000000000000000", negOnePointFive.ToString());
    }

    [TestMethod]
    public void ToString_PositiveOnePointSevenFive_SameAsToHexString()
    {
        // 1.75 = 7 >> 2
        Assert.AreEqual("1.C0000000000000000000000000000000", new Fixed(7).ShiftRight(2).ToString());
    }

    [TestMethod]
    public void ToString_NegativeDisplayIsMagnitudeOfNegate()
    {
        // For any negative x, ToString() == "-" + x.Negate().ToHexString()
        var x = new Fixed(-3).ShiftRight(1); // -1.5
        Assert.AreEqual("-" + x.Negate().ToHexString(), x.ToString());
    }
}
