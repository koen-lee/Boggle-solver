namespace FixedPoint.Tests;

/// <summary>
/// Tests for ToDecimalString(). Fixed fractional precision: 39 digits
/// (= ceil(128 * log10(2))), matching the hex variant's policy of showing all bits.
/// Trailing zeros are kept, just like ToHexString().
/// </summary>
[TestClass]
public sealed class DecimalStringTests
{
    const string ZeroFrac = "000000000000000000000000000000000000000"; // 39 zeros

    [TestMethod]
    public void ToDecimalString_Zero()
    {
        Assert.AreEqual("0." + ZeroFrac, Fixed.Zero.ToDecimalString());
    }

    [TestMethod]
    public void ToDecimalString_PositiveInteger()
    {
        Assert.AreEqual("7." + ZeroFrac, new Fixed(7).ToDecimalString());
    }

    [TestMethod]
    public void ToDecimalString_MultiDigitInteger()
    {
        // 3 * 5 = 15, exercises integer part > 9
        Assert.AreEqual("15." + ZeroFrac, new Fixed(3).Multiply(new Fixed(5)).ToDecimalString());
    }

    [TestMethod]
    public void ToDecimalString_NegativeInteger()
    {
        Assert.AreEqual("-3." + ZeroFrac, new Fixed(-3).ToDecimalString());
    }

    [TestMethod]
    public void ToDecimalString_Half()
    {
        // 0.5 = 0x80000000 in the MSB fraction word
        var half = Fixed.ParseHexStringExact("0.80000000000000000000000000000000");
        Assert.AreEqual("0.500000000000000000000000000000000000000", half.ToDecimalString());
    }

    [TestMethod]
    public void ToDecimalString_Quarter()
    {
        var quarter = Fixed.ParseHexStringExact("0.40000000000000000000000000000000");
        Assert.AreEqual("0.250000000000000000000000000000000000000", quarter.ToDecimalString());
    }

    [TestMethod]
    public void ToDecimalString_Eighth()
    {
        var eighth = Fixed.ParseHexStringExact("0.20000000000000000000000000000000");
        Assert.AreEqual("0.125000000000000000000000000000000000000", eighth.ToDecimalString());
    }

    [TestMethod]
    public void ToDecimalString_OneAndHalf()
    {
        var oneAndHalf = Fixed.ParseHexStringExact("1.80000000000000000000000000000000");
        Assert.AreEqual("1.500000000000000000000000000000000000000", oneAndHalf.ToDecimalString());
    }

    [TestMethod]
    public void ToDecimalString_NegativeHalf()
    {
        // -0.5: IntegerPart = -1 (FFFFFFFF), fraction = 0x80000000 — two's complement representation.
        // ToDecimalString must output "-0.5...", not "-1.5...".
        var negHalf = Fixed.ParseHexStringExact("FFFFFFFF.80000000000000000000000000000000");
        Assert.AreEqual("-0.500000000000000000000000000000000000000", negHalf.ToDecimalString());
    }

    [TestMethod]
    public void ToDecimalString_NegativeThreeQuarters()
    {
        // -0.75: Negate(0.75) → words[3]=0x40000000, integer=0xFFFFFFFF=-1.
        // (0.75 = 0xC0000000 MSB fraction word; two's complement flip+1 gives 0x40000000 with integer=-1.)
        // ToDecimalString must output "-0.75...", not "-1.25...".
        var negThreeQuarters = Fixed.ParseHexStringExact("FFFFFFFF.40000000000000000000000000000000");
        Assert.AreEqual("-0.750000000000000000000000000000000000000", negThreeQuarters.ToDecimalString());
    }


    [TestMethod]
    public void ToDecimalString_SmallPi()
    {
        // Math.PI / 10^30 = 3.14159...e-30, which has 29 leading zeros then the pi digits.
        // Double precision gives ~16 significant digits; beyond the 39th char the bits are zero.
        var underTest = new Fixed(Math.PI / Math.Pow(10, 30));
        Assert.AreEqual("0.000000000000000000000000000003141592653", underTest.ToDecimalString());
    }

    [TestMethod]
    public void ToDecimalString_NegativeOneAndHalf()
    {
        // -1.5: IntegerPart = -2 (FFFFFFFE), fraction = 0x80000000
        var negOneAndHalf = Fixed.ParseHexStringExact("FFFFFFFE.80000000000000000000000000000000");
        Assert.AreEqual("-1.500000000000000000000000000000000000000", negOneAndHalf.ToDecimalString());
    }

    [TestMethod]
    public void ToDecimalString_NegativeThree()
    {
        Assert.AreEqual("-3." + ZeroFrac, new Fixed(-3).ToDecimalString());
    }

    [TestMethod]
    public void ToDecimalString_FractionalDigitCount()
    {
        // Regardless of value, fractional part is always exactly 39 digits.
        var s = new Fixed(42).ToDecimalString();
        var dot = s.IndexOf('.');
        Assert.AreEqual(39, s.Length - dot - 1);
    }

    [TestMethod]
    public void ToDecimalString_MatchesNegateSymmetry()
    {
        // ToString(x) and ToString(-x) should differ only in the leading '-'.
        var x = Fixed.ParseHexStringExact("3.40000000000000000000000000000000"); // 3.25
        var neg = x.Negate();
        Assert.AreEqual("-" + x.ToDecimalString(), neg.ToDecimalString());
    }
}
