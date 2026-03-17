namespace FixedPoint.Tests;

[TestClass]
public sealed class MultiplyTests
{
    [TestMethod]
    public void Multiply_TwoPositiveIntegers()
    {
        Assert.AreEqual("F.00000000000000000000000000000000", new Fixed(3).Multiply(new Fixed(5)).ToHexString());
    }

    [TestMethod]
    public void Multiply_ByZero_ReturnsZero()
    {
        Assert.AreEqual(Fixed.Zero.ToHexString(), new Fixed(7).Multiply(Fixed.Zero).ToHexString());
    }

    [TestMethod]
    public void Multiply_ByOne_ReturnsOriginal()
    {
        var a = new Fixed(6);
        Assert.AreEqual(a.ToHexString(), a.Multiply(new Fixed(1)).ToHexString());
    }

    [TestMethod]
    public void Multiply_PositiveByNegative_IsNegative()
    {
        var result = new Fixed(3).Multiply(new Fixed(-4));
        Assert.IsTrue(result.CompareTo(Fixed.Zero) < 0);
    }

    [TestMethod]
    public void Multiply_NegativeByNegative_IsPositive()
    {
        var result = new Fixed(-3).Multiply(new Fixed(-4));
        Assert.IsTrue(result.CompareTo(Fixed.Zero) > 0);
        Assert.AreEqual(new Fixed(3).Multiply(new Fixed(4)).ToHexString(), result.ToHexString());
    }

    [TestMethod]
    public void Multiply_IsCommutative()
    {
        var a = new Fixed(6);
        var b = new Fixed(7);
        Assert.AreEqual(a.Multiply(b).ToHexString(), b.Multiply(a).ToHexString());
    }

    [TestMethod]
    public void Multiply_HalfByTwo_ReturnsOne()
    {
        // 0.5 * 2 = 1
        var half = Fixed.ParseHexStringExact("0.80000000000000000000000000000000");
        Assert.AreEqual("1.00000000000000000000000000000000", half.Multiply(new Fixed(2)).ToHexString());
    }

    [TestMethod]
    public void Multiply_HalfByHalf_ReturnsQuarter()
    {
        // 0.5 * 0.5 = 0.25
        var half = Fixed.ParseHexStringExact("0.80000000000000000000000000000000");
        Assert.AreEqual("0.40000000000000000000000000000000", half.Multiply(half).ToHexString());
    }

    [TestMethod]
    public void Multiply_ByNegativeOne_EqualsNegate()
    {
        var a = new Fixed(5);
        Assert.AreEqual(a.Negate().ToHexString(), a.Multiply(new Fixed(-1)).ToHexString());
    }

    [TestMethod]
    public void Multiply_LowBitsCarryIntoResult()
    {
        // (0.5 + 2^-128)^2 = 0.25 + 2^-128 + 2^-256.
        // The 2^-256 term is below precision. The cross-term a[0]*b[3] + a[3]*b[0]
        // = 0x80000000 + 0x80000000 = 0x100000000 on diagonal 3 (below the result
        // window), producing carry=1 that must propagate into the result's LSW.
        var a = Fixed.ParseHexStringExact("0.80000000000000000000000000000001");
        Assert.AreEqual("0.40000000000000000000000000000001", a.Multiply(a).ToHexString());
    }
}
