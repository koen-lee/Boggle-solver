namespace FixedPoint.Tests;

[TestClass]
public sealed class DoubleConversionTests
{
    // --- Fixed(double) constructor ---

    [TestMethod]
    public void FromDouble_Zero_IsFixedZero()
    {
        Assert.AreEqual("0.00000000000000000000000000000000", new Fixed(0.0).ToHexString());
    }

    [TestMethod]
    public void FromDouble_NegativeZero_IsFixedZero()
    {
        Assert.AreEqual("0.00000000000000000000000000000000", new Fixed(-0.0).ToHexString());
    }

    [TestMethod]
    public void FromDouble_One_IsExact()
    {
        Assert.AreEqual("1.00000000000000000000000000000000", new Fixed(1.0).ToHexString());
    }

    [TestMethod]
    public void FromDouble_Two_IsExact()
    {
        Assert.AreEqual("2.00000000000000000000000000000000", new Fixed(2.0).ToHexString());
    }

    [TestMethod]
    public void FromDouble_Half_IsExact()
    {
        // 0.5 = 0x80000000 in fraction[0]
        Assert.AreEqual("0.80000000000000000000000000000000", new Fixed(0.5).ToHexString());
    }

    [TestMethod]
    public void FromDouble_Quarter_IsExact()
    {
        // 0.25 = 0x40000000 in fraction[0]
        Assert.AreEqual("0.40000000000000000000000000000000", new Fixed(0.25).ToHexString());
    }

    [TestMethod]
    public void FromDouble_OnePointFive_IsExact()
    {
        Assert.AreEqual("1.80000000000000000000000000000000", new Fixed(1.5).ToHexString());
    }

    [TestMethod]
    public void FromDouble_NegativeOne_IsExact()
    {
        Assert.AreEqual("FFFFFFFF.00000000000000000000000000000000", new Fixed(-1.0).ToHexString());
    }

    [TestMethod]
    public void FromDouble_NegativeOnePointFive_IsExact()
    {
        Assert.AreEqual("FFFFFFFE.80000000000000000000000000000000", new Fixed(-1.5).ToHexString());
    }

    [TestMethod]
    public void FromDouble_NaN_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = new Fixed(double.NaN));
    }

    [TestMethod]
    public void FromDouble_PositiveInfinity_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = new Fixed(double.PositiveInfinity));
    }

    [TestMethod]
    public void FromDouble_NegativeInfinity_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = new Fixed(double.NegativeInfinity));
    }

    // --- explicit operator double ---

    [TestMethod]
    public void ToDouble_Zero_IsZero()
    {
        Assert.AreEqual(0.0, (double)Fixed.Zero);
    }

    [TestMethod]
    public void ToDouble_One_IsExact()
    {
        Assert.AreEqual(1.0, (double)new Fixed(1.0));
    }

    [TestMethod]
    public void ToDouble_NegativeOnePointFive_IsExact()
    {
        Assert.AreEqual(-1.5, (double)new Fixed(-1.5));
    }

    // --- Round-trip: double → Fixed → double ---

    [TestMethod]
    public void RoundTrip_One()
    {
        Fixed f = new(1.0);
        Assert.AreEqual(1.0, (double)f);
    }

    [TestMethod]
    public void RoundTrip_OnePointFive()
    {
        Fixed f = new(1.5);
        Assert.AreEqual(1.5, (double)f);
    }

    [TestMethod]
    public void RoundTrip_NegativeThreeQuarters()
    {
        Fixed f = new(-0.75);
        Assert.AreEqual(-0.75, (double)f);
    }

    [TestMethod]
    public void RoundTrip_Pi()
    {
        Fixed f = new(Math.PI);
        Assert.AreEqual(Math.PI, (double)f);
    }

    [TestMethod]
    public void RoundTrip_LargeInteger()
    {
        Fixed f = new(1000000.0);
        Assert.AreEqual(1000000.0, (double)f);
    }

    [TestMethod]
    public void RoundTrip_SmallFraction()
    {
        // 2^-20 is exactly representable
        var v = 1.0 / (1 << 20);
        Fixed f = new(v);
        Assert.AreEqual(v, (double)f);
    }
}
