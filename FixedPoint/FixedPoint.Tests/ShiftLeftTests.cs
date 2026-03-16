namespace FixedPoint.Tests;

[TestClass]
public sealed class ShiftLeftTests
{
    [TestMethod]
    public void ShiftLeft_ByZero_ReturnsSame()
    {
        var a = new Fixed(3);
        Assert.AreEqual(a.ToHexString(), a.ShiftLeft(0).ToHexString());
    }

    [TestMethod]
    public void ShiftLeft_PositiveInteger_Doubles()
    {
        Assert.AreEqual("6.00000000000000000000000000000000", new Fixed(3).ShiftLeft(1).ToHexString());
    }

    [TestMethod]
    public void ShiftLeft_PositiveInteger_ByTwo_Quadruples()
    {
        Assert.AreEqual("C.00000000000000000000000000000000", new Fixed(3).ShiftLeft(2).ToHexString());
    }

    [TestMethod]
    public void ShiftLeft_NegativeInteger_Doubles()
    {
        // -3 << 1 = -6 (0xFFFFFFFA)
        Assert.AreEqual("FFFFFFFA.00000000000000000000000000000000", new Fixed(-3).ShiftLeft(1).ToHexString());
    }

    // --- Round-trip with ShiftRight ---

    [TestMethod]
    public void ShiftLeft_AfterShiftRight_RecoversFractionalBits()
    {
        // 5 >> 1 = 2.5, then << 1 = 5
        Assert.AreEqual(new Fixed(5).ToHexString(), new Fixed(5).ShiftRight(1).ShiftLeft(1).ToHexString());
    }

    [TestMethod]
    public void ShiftLeft_AfterShiftRight_ByTwo_RecoversFractionalBits()
    {
        // 7 >> 2 = 1.75, then << 2 = 7
        Assert.AreEqual(new Fixed(7).ToHexString(), new Fixed(7).ShiftRight(2).ShiftLeft(2).ToHexString());
    }

    [TestMethod]
    public void ShiftLeft_AfterShiftRight_Negative_RoundTrips()
    {
        // -3 >> 1 = -1.5, then << 1 = -3
        Assert.AreEqual(new Fixed(-3).ToHexString(), new Fixed(-3).ShiftRight(1).ShiftLeft(1).ToHexString());
    }

    // --- Fraction bits promote into integer ---

    [TestMethod]
    public void ShiftLeft_ByOne_TopFractionBitPromotesToInteger()
    {
        // 0.5 (fraction[0]=0x80000000) << 1 = 1.0
        var half = Fixed.ParseHexStringExact("0.80000000000000000000000000000000");
        Assert.AreEqual("1.00000000000000000000000000000000", half.ShiftLeft(1).ToHexString());
    }

    [TestMethod]
    public void ShiftLeft_ByTwo_TopTwoFractionBitsPromote()
    {
        // 0.75 (fraction[0]=0xC0000000) << 2 = 3.0
        var threeQuarters = Fixed.ParseHexStringExact("0.C0000000000000000000000000000000");
        Assert.AreEqual("3.00000000000000000000000000000000", threeQuarters.ShiftLeft(2).ToHexString());
    }

    // --- Whole-element shifts ---

    [TestMethod]
    public void ShiftLeft_By32_FractionElement0PromotesToInteger()
    {
        // 5/2^32 << 32 = 5
        var fiveOver2e32 = Fixed.ParseHexStringExact("0.00000005000000000000000000000000");
        Assert.AreEqual("5.00000000000000000000000000000000", fiveOver2e32.ShiftLeft(32).ToHexString());
    }

    [TestMethod]
    public void ShiftLeft_By33_WholeElementThenRemainder()
    {
        // 5/2^33 << 33 = 5; verify two-step compose is inverse of ShiftRight(33)
        Assert.AreEqual(new Fixed(5).ToHexString(), new Fixed(5).ShiftRight(33).ShiftLeft(33).ToHexString());
    }

    [TestMethod]
    public void ShiftLeft_By64_FractionElement1PromotesToInteger()
    {
        // 1/2^64 << 64 = 1
        var oneOver2e64 = Fixed.ParseHexStringExact("0.00000000000000010000000000000000");
        Assert.AreEqual("1.00000000000000000000000000000000", oneOver2e64.ShiftLeft(64).ToHexString());
    }

    [TestMethod]
    public void ShiftLeft_BeyondPrecision_ReturnsZero()
    {
        // Shifting beyond total precision (32+128=160 bits) → zero
        Assert.AreEqual("0.00000000000000000000000000000000", new Fixed(1).ShiftLeft(160).ToHexString());
    }
}
