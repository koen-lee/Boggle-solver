namespace FixedPoint.Tests;

[TestClass]
public sealed class NegateTests
{
    [TestMethod]
    public void Negate_Zero_ReturnsZero()
    {
        Assert.AreEqual("0.00000000000000000000000000000000", Fixed.Zero.Negate().ToHexString());
    }

    [TestMethod]
    public void Negate_PositiveInteger_ReturnsNegative()
    {
        // -5 in two's complement is 0xFFFFFFFB
        Assert.AreEqual("FFFFFFFB.00000000000000000000000000000000", new Fixed(5).Negate().ToHexString());
    }

    [TestMethod]
    public void Negate_NegativeInteger_ReturnsPositive()
    {
        Assert.AreEqual("5.00000000000000000000000000000000", new Fixed(-5).Negate().ToHexString());
    }

    [TestMethod]
    public void Negate_NegativeOne_ReturnsOne()
    {
        Assert.AreEqual("1.00000000000000000000000000000000", new Fixed(-1).Negate().ToHexString());
    }

    [TestMethod]
    public void Negate_PositiveFractional_ReturnsNegativeFractional()
    {
        // 0.5 (fraction[0]=0x80000000) → -0.5 (integer=-1, fraction[0]=0x80000000)
        var half = Fixed.ParseHexStringExact("0.80000000000000000000000000000000");
        Assert.AreEqual("FFFFFFFF.80000000000000000000000000000000", half.Negate().ToHexString());
    }

    [TestMethod]
    public void Negate_NegativeFractional_ReturnsPositiveFractional()
    {
        // -0.5 → 0.5
        var negHalf = Fixed.ParseHexStringExact("FFFFFFFF.80000000000000000000000000000000");
        Assert.AreEqual("0.80000000000000000000000000000000", negHalf.Negate().ToHexString());
    }

    [TestMethod]
    public void Negate_IsInvolution()
    {
        // Double negation returns original
        var original = new Fixed(42);
        Assert.AreEqual(original.ToHexString(), original.Negate().Negate().ToHexString());
    }

    [TestMethod]
    public void Negate_FractionalCarry_PropagatesIntoInteger()
    {
        // -1.5 in raw hex: integer=-2 (FFFFFFFE), fraction[0]=0x80000000
        // Negate should give +1.5: integer=1, fraction[0]=0x80000000
        var negOnePointFive = new Fixed(-3).ShiftRight(1);
        Assert.AreEqual("1.80000000000000000000000000000000", negOnePointFive.Negate().ToHexString());
    }
}
