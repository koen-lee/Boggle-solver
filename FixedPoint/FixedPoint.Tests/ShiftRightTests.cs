namespace FixedPoint.Tests;

[TestClass]
public sealed class ShiftRightTests
{
    [TestMethod]
    public void ShiftRight_ByZero_ReturnsSame()
    {
        var a = new Fixed(8);
        Assert.AreEqual(a.ToHexString(), a.ShiftRight(0).ToHexString());
    }

    [TestMethod]
    public void ShiftRight_EvenPositive_ByOne_HalvesExactly()
    {
        // 8 (LSB=0) >> 1 = 4.0, no fractional carry
        var result = new Fixed(8).ShiftRight(1);
        Assert.AreEqual(4, Helpers.IntegerPart(result));
        Assert.AreEqual("4.00000000000000000000000000000000", result.ToHexString());
    }

    [TestMethod]
    public void ShiftRight_EvenPositive_ByTwo_QuartersExactly()
    {
        // 8 (lower 2 bits = 0) >> 2 = 2.0, no fractional carry
        var result = new Fixed(8).ShiftRight(2);
        Assert.AreEqual(2, Helpers.IntegerPart(result));
        Assert.AreEqual("2.00000000000000000000000000000000", result.ToHexString());
    }

    // --- Signed / two's complement carry into fraction ---
    // The LSBs of the integer become the MSBs of the fraction.
    // Odd integers (LSB=1) produce a non-zero fractional part; even integers do not.
    // Two's complement means negative numbers follow the same parity rule.

    [TestMethod]
    public void ShiftRight_OddPositive_ByOne_CarriesHalfIntoFraction()
    {
        // 5 (binary ...0101, LSB=1) >> 1 = 2.5
        Assert.AreEqual("2.80000000000000000000000000000000", new Fixed(5).ShiftRight(1).ToHexString());
    }

    [TestMethod]
    public void ShiftRight_EvenPositive_ByOne_NoFractionalCarry()
    {
        // 4 (binary ...0100, LSB=0) >> 1 = 2.0
        Assert.AreEqual("2.00000000000000000000000000000000", new Fixed(4).ShiftRight(1).ToHexString());
    }

    [TestMethod]
    public void ShiftRight_OddPositive_ByTwo_CarriesTwoLSBsIntoFraction()
    {
        // 7 (binary ...0111, lower 2 bits = 11) >> 2 = 1.75 → fraction[0]=0xC0000000
        Assert.AreEqual("1.C0000000000000000000000000000000", new Fixed(7).ShiftRight(2).ToHexString());
    }

    [TestMethod]
    public void ShiftRight_NegativeOdd_ByOne_CarriesHalfIntoFraction()
    {
        // -1 (0xFFFFFFFF, LSB=1) >> 1: integer stays -1, fraction[0] MSB=1
        Assert.AreEqual("FFFFFFFF.80000000000000000000000000000000", new Fixed(-1).ShiftRight(1).ToHexString());
    }

    [TestMethod]
    public void ShiftRight_NegativeEven_ByOne_NoFractionalCarry()
    {
        // -2 (0xFFFFFFFE, LSB=0) >> 1: integer=-1, no fractional carry
        Assert.AreEqual("FFFFFFFF.00000000000000000000000000000000", new Fixed(-2).ShiftRight(1).ToHexString());
    }

    [TestMethod]
    public void ShiftRight_NegativeOdd_ByOne_HasCorrectIntegerAndFraction()
    {
        // -3 (0xFFFFFFFD, LSB=1) >> 1 = -1.5: integer=-2 (FFFFFFFE), fraction[0]=0x80000000
        Assert.AreEqual("FFFFFFFE.80000000000000000000000000000000", new Fixed(-3).ShiftRight(1).ToHexString());
    }

    [TestMethod]
    public void ShiftRight_NegativeEven_ByOne_NoFractionalCarry2()
    {
        // -4 (0xFFFFFFFC, LSB=0) >> 1 = -2.0
        Assert.AreEqual("FFFFFFFE.00000000000000000000000000000000", new Fixed(-4).ShiftRight(1).ToHexString());
    }

    [TestMethod]
    public void ShiftRight_NegativeEven_ByFour_FractionalCarry()
    {
        // -4 (0xFFFFFFFC) >> 4 = 0xFFFFFFFF.C...
        var underTest = unchecked((int)0xFFFFFFFC);
        Assert.AreEqual(-4, underTest);
        Assert.AreEqual("FFFFFFFF.C0000000000000000000000000000000", new Fixed(underTest).ShiftRight(4).ToHexString());
    }

    [TestMethod]
    public void ShiftRight_NegativeInteger_ArithmeticShift()
    {
        // -4 >> 1 = -2 (arithmetic right shift preserves sign)
        Assert.AreEqual(-2, Helpers.IntegerPart(new Fixed(-4).ShiftRight(1)));
    }

    [TestMethod]
    public void ShiftRight_NegativeOne_SignExtends()
    {
        // -1 >> 1: integer part stays -1 (sign extended)
        Assert.AreEqual(-1, Helpers.IntegerPart(new Fixed(-1).ShiftRight(1)));
    }

    // --- Shifts >= 32 bits (whole-element slide) ---

    [TestMethod]
    public void ShiftRight_By32_IntegerBitsLandInFraction0()
    {
        // 5 >> 32 = 5/2^32; integer becomes 0, fraction[0] = (uint)5
        Assert.AreEqual("0.00000005000000000000000000000000", new Fixed(5).ShiftRight(32).ToHexString());
    }

    [TestMethod]
    public void ShiftRight_NegativeBy32_SignFillsGapAndIntegerMovesToFraction()
    {
        // -65536 (0xFFFF0000) >> 32: integer=-1, fraction[0]=0xFFFF0000
        Assert.AreEqual("FFFFFFFF.FFFF0000000000000000000000000000", new Fixed(-0x10000).ShiftRight(32).ToHexString());
    }

    [TestMethod]
    public void ShiftRight_By33_WholeElementThenRemainder()
    {
        // 5 >> 33: after 32-bit slide fraction[0]=5, then >>1 → fraction[0]=2, fraction[1]=0x80000000
        Assert.AreEqual("0.00000002800000000000000000000000", new Fixed(5).ShiftRight(33).ToHexString());
    }

    [TestMethod]
    public void ShiftRight_By64_IntegerLandsAtFraction1WithSignFillAtFraction0()
    {
        // 1 >> 64: fraction[0]=0 (sign fill), fraction[1]=(uint)1
        Assert.AreEqual("0.00000000000000010000000000000000", new Fixed(1).ShiftRight(64).ToHexString());
    }

    [TestMethod]
    public void ShiftRight_LargePositive_AllBitsShiftedOut_ReturnsZero()
    {
        Assert.AreEqual("0.00000000000000000000000000000000", new Fixed(1).ShiftRight(160).ToHexString());
    }

    [TestMethod]
    public void ShiftRight_LargeNegative_AllBitsShiftedOut_ReturnsNegativeEpsilon()
    {
        Assert.AreEqual("FFFFFFFF.FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF", new Fixed(-1).ShiftRight(160).ToHexString());
    }

    // --- ParseHexStringExact roundtrip ---

    [TestMethod]
    public void ParseHexStringExact_RoundtripsWithToHexString()
    {
        var original = new Fixed(7).ShiftRight(2); // 1.75
        Assert.AreEqual(original.ToHexString(), Fixed.ParseHexStringExact(original.ToHexString()).ToHexString());
    }

    [TestMethod]
    public void ParseHexStringExact_RoundtripsNegativeFractional()
    {
        var original = new Fixed(-3).ShiftRight(1); // -1.5 in two's complement
        Assert.AreEqual(original.ToHexString(), Fixed.ParseHexStringExact(original.ToHexString()).ToHexString());
    }
}
