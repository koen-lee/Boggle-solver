using FluentAssertions;

namespace FixedPoint.Tests;

[TestClass]
public sealed class MultiplyTests
{
    [TestMethod]
    public void Multiply_TwoPositiveIntegers()
    {
        new Fixed(3).Multiply(new Fixed(5)).ToHexString().Should().Be("F.00000000000000000000000000000000");
    }

    [TestMethod]
    public void Multiply_ByZero_ReturnsZero()
    {
        new Fixed(7).Multiply(Fixed.Zero).ToHexString().Should().Be(Fixed.Zero.ToHexString());
    }

    [TestMethod]
    public void Multiply_ByOne_ReturnsOriginal()
    {
        var a = new Fixed(6);
        a.Multiply(new Fixed(1)).ToHexString().Should().Be(a.ToHexString());
    }

    [TestMethod]
    public void Multiply_PositiveByNegative_IsNegative()
    {
        var result = new Fixed(3).Multiply(new Fixed(-4));
        result.CompareTo(Fixed.Zero).Should().BeNegative();
    }

    [TestMethod]
    public void Multiply_NegativeByNegative_IsPositive()
    {
        var result = new Fixed(-3).Multiply(new Fixed(-4));
        result.CompareTo(Fixed.Zero).Should().BePositive();
        result.ToHexString().Should().Be(new Fixed(3).Multiply(new Fixed(4)).ToHexString());
    }

    [TestMethod]
    public void Multiply_IsCommutative()
    {
        var a = new Fixed(6);
        var b = new Fixed(7);
        a.Multiply(b).ToHexString().Should().Be(b.Multiply(a).ToHexString());
    }

    [TestMethod]
    public void Multiply_HalfByTwo_ReturnsOne()
    {
        // 0.5 * 2 = 1
        var half = Fixed.ParseHexStringExact("0.80000000000000000000000000000000");
        half.Multiply(new Fixed(2)).ToHexString().Should().Be("1.00000000000000000000000000000000");
    }

    [TestMethod]
    public void Multiply_HalfByHalf_ReturnsQuarter()
    {
        // 0.5 * 0.5 = 0.25
        var half = Fixed.ParseHexStringExact("0.80000000000000000000000000000000");
        half.Multiply(half).ToHexString().Should().Be("0.40000000000000000000000000000000");
    }

    [TestMethod]
    public void Multiply_ByNegativeOne_EqualsNegate()
    {
        var a = new Fixed(5);
        a.Multiply(new Fixed(-1)).ToHexString().Should().Be(a.Negate().ToHexString());
    }

    [TestMethod]
    public void Multiply_LowBitsCarryIntoResult()
    {
        // (0.5 + 2^-128)^2 = 0.25 + 2^-128 + 2^-256.
        // The 2^-256 term is below precision. The cross-term a[0]*b[3] + a[3]*b[0]
        // = 0x80000000 + 0x80000000 = 0x100000000 on diagonal 3 (below the result
        // window), producing carry=1 that must propagate into the result's LSW.
        var a = Fixed.ParseHexStringExact("0.80000000000000000000000000000001");
        a.Multiply(a).ToHexString().Should().Be("0.40000000000000000000000000000001");
    }
}
