namespace FixedPoint.Tests;

[TestClass]
public sealed class ShiftRightTests
{
    [TestMethod]
    public void ShiftRight_ByZero_ReturnsSame()
    {
        var a = new Fixed(8);
        Assert.AreEqual(a.ToString(), a.ShiftRight(0).ToString());
    }

    [TestMethod]
    public void ShiftRight_EvenPositive_ByOne_HalvesExactly()
    {
        // 8 (LSB=0) >> 1 = 4.0, no fractional carry
        var result = new Fixed(8).ShiftRight(1);
        Assert.AreEqual(4, Helpers.IntegerPart(result));
        Assert.AreEqual($"4.{Helpers.ZeroFraction}", result.ToString());
    }

    [TestMethod]
    public void ShiftRight_EvenPositive_ByTwo_QuartersExactly()
    {
        // 8 (lower 2 bits = 0) >> 2 = 2.0, no fractional carry
        var result = new Fixed(8).ShiftRight(2);
        Assert.AreEqual(2, Helpers.IntegerPart(result));
        Assert.AreEqual($"2.{Helpers.ZeroFraction}", result.ToString());
    }

    // --- Signed / two's complement carry into fraction ---
    // The LSBs of the integer become the MSBs of the fraction.
    // Odd integers (LSB=1) produce a non-zero fractional part; even integers do not.
    // Two's complement means negative numbers follow the same parity rule.

    [TestMethod]
    public void ShiftRight_OddPositive_ByOne_CarriesHalfIntoFraction()
    {
        // 5 (binary ...0101, LSB=1) >> 1 = 2.5
        // integer=2, fraction[0] MSB=1 (0x80000000)
        var result = new Fixed(5).ShiftRight(1);
        Assert.AreEqual($"2.80000000{Helpers.ZeroFraction[..24]}", result.ToString());
    }

    [TestMethod]
    public void ShiftRight_EvenPositive_ByOne_NoFractionalCarry()
    {
        // 4 (binary ...0100, LSB=0) >> 1 = 2.0
        var result = new Fixed(4).ShiftRight(1);
        Assert.AreEqual($"2.{Helpers.ZeroFraction}", result.ToString());
    }

    [TestMethod]
    public void ShiftRight_OddPositive_ByTwo_CarriesTwoLSBsIntoFraction()
    {
        // 7 (binary ...0111, lower 2 bits = 11) >> 2 = 1.75
        // integer=1, top 2 fraction bits set (0xC0000000)
        var result = new Fixed(7).ShiftRight(2);
        Assert.AreEqual($"1.C0000000{Helpers.ZeroFraction[..24]}", result.ToString());
    }

    [TestMethod]
    public void ShiftRight_NegativeOdd_ByOne_CarriesHalfIntoFraction()
    {
        // -1 (two's complement 0xFFFFFFFF, LSB=1) >> 1 = -0.5
        // integer=-1 (FFFFFFFF), fraction[0]=0x80000000
        var result = new Fixed(-1).ShiftRight(1);
        Assert.AreEqual($"FFFFFFFF.80000000{Helpers.ZeroFraction[..24]}", result.ToString());
    }

    [TestMethod]
    public void ShiftRight_NegativeEven_ByOne_NoFractionalCarry()
    {
        // -2 (two's complement 0xFFFFFFFE, LSB=0) >> 1 = -1.0
        var result = new Fixed(-2).ShiftRight(1);
        Assert.AreEqual($"FFFFFFFF.{Helpers.ZeroFraction}", result.ToString());
    }

    [TestMethod]
    public void ShiftRight_NegativeOdd_ByOne_HasCorrectIntegerAndFraction()
    {
        // -3 (two's complement 0xFFFFFFFD, LSB=1) >> 1 = -1.5
        // integer=-2 (FFFFFFFE), fraction[0] MSB=1 (0x80000000)
        var result = new Fixed(-3).ShiftRight(1);
        Assert.AreEqual($"FFFFFFFE.80000000{Helpers.ZeroFraction[..24]}", result.ToString());
    }

    [TestMethod]
    public void ShiftRight_NegativeEven_ByOne_NoFractionalCarry2()
    {
        // -4 (two's complement 0xFFFFFFFC, LSB=0) >> 1 = -2.0
        var result = new Fixed(-4).ShiftRight(1);
        Assert.AreEqual($"FFFFFFFE.{Helpers.ZeroFraction}", result.ToString());
    }

    [TestMethod]
    public void ShiftRight_NegativeEven_ByFour_FractionalCarry()
    {
        // shifting by 4 is easy in hex
        // -4 (two's complement 0xFFFFFFFC) >> 4 = 0xFFFFFFFF.C
        var underTest = unchecked((int)0xFFFFFFFC);
        Assert.AreEqual(-4, underTest);
        var result = new Fixed(underTest).ShiftRight(4);
        // sign extended -v        v- msbs are shifted into fraction
        Assert.AreEqual($"FFFFFFFF.C{Helpers.ZeroFraction[..31]}", result.ToString());
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
        // 5 >> 32 = 5/2^32; integer becomes 0 (positive sign), fraction[0] = (uint)5
        var result = new Fixed(5).ShiftRight(32);
        Assert.AreEqual($"0.00000005{Helpers.ZeroFraction[..24]}", result.ToString());
    }

    [TestMethod]
    public void ShiftRight_NegativeBy32_SignFillsGapAndIntegerMovesToFraction()
    {
        // -65536 (0xFFFF0000) >> 32: integer=-1, fraction[0]=0xFFFF0000, rest=0
        var result = new Fixed(-0x10000).ShiftRight(32);
        Assert.AreEqual($"FFFFFFFF.FFFF0000{Helpers.ZeroFraction[..24]}", result.ToString());
    }

    [TestMethod]
    public void ShiftRight_By33_WholeElementThenRemainder()
    {
        // 5 >> 33 = 5/2^33
        // After 32-bit slide: fraction[0]=5; remainder shift of 1 produces fraction[0]=2, fraction[1]=0x80000000
        var result = new Fixed(5).ShiftRight(33);
        Assert.AreEqual($"0.0000000280000000{Helpers.ZeroFraction[..16]}", result.ToString());
    }

    [TestMethod]
    public void ShiftRight_By64_IntegerLandsAtFraction1WithSignFillAtFraction0()
    {
        // 1 >> 64: fraction[0]=0 (sign fill), fraction[1]=(uint)1, rest=0
        var result = new Fixed(1).ShiftRight(64);
        Assert.AreEqual($"0.{Helpers.ZeroFraction[..8]}00000001{Helpers.ZeroFraction[..16]}", result.ToString());
    }

    [TestMethod]
    public void ShiftRight_LargePositive_AllBitsShiftedOut_ReturnsZero()
    {
        // Shifting a positive value further than the total precision (32+128=160 bits) → all zero
        var result = new Fixed(1).ShiftRight(160);
        Assert.AreEqual($"0.{Helpers.ZeroFraction}", result.ToString());
    }

    [TestMethod]
    public void ShiftRight_LargeNegative_AllBitsShiftedOut_ReturnsNegativeEpsilon()
    {
        // Shifting -1 further than total precision → all bits are sign (all 1s)
        var result = new Fixed(-1).ShiftRight(160);
        Assert.AreEqual($"FFFFFFFF.FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF", result.ToString());
    }
}
