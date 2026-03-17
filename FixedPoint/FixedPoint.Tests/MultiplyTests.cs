using FluentAssertions;

namespace FixedPoint.Tests;

[TestClass]
public sealed class KaratsubaMultiplyTests
{
    // Parses a raw word-array hex string (MSB-first, no dot, 8*(Value+1) chars) into a Fixed<TSize>.
    private static Fixed<TSize> FromWordHex<TSize>(string h) where TSize : struct, IFixedSize
    {
        var intPart = Convert.ToUInt32(h[..8], 16);
        return Fixed<TSize>.ParseHexStringExact($"{intPart:X}.{h[8..]}");
    }

    // The exact operands of the first n=32 Karatsuba/schoolbook disagreement,
    // captured by OnMismatch32 during Sqrt<Size31>(2).
    private const string MismatchA = "00000000B504F333F9DE6108B2FB1366EAA6A543449E1462529C9CA82CA7AAC656F500AB1C2FB0C1802CEE1C9D6C2B33ED905235027516242101BC952C2B9CA147350E0FFA213B6D7063CB25CF8EA00CF1B3529FE6ECD2F3A13CA0D461045DE02C1A52BBB0000000000000000000000000000000000000000000000000000000";
    private const string MismatchB = "000000010000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000014E750A32DFBAB84CB0C77827A0FE07C2CFD2B228A42BE6031EB7ADC113BDD88C71717FD4BC8ED1A2004D762A9E2E241000000000000000000000000000000000000000000";

    [TestMethod]
    public void Karatsuba_n32_MatchesSchoolbook_FirstMismatchInputs()
    {
        var rK = (FromWordHex<Size31>          (MismatchA) * FromWordHex<Size31>          (MismatchB)).ToHexString();
        var rS = (FromWordHex<Size31Schoolbook>(MismatchA) * FromWordHex<Size31Schoolbook>(MismatchB)).ToHexString();
        rK.Should().Be(rS);
    }

    // Builds a Size255 hex string: "intPart.<msb><zeros><lsb>"
    // msb and lsb are 8-char hex words; zeros fills the remaining 253 fraction words.
    private static Fixed<Size255> MakeSize255(int intPart, string msbWord, string lsbWord)
    {
        var frac = msbWord + new string('0', 253 * 8) + lsbWord;
        return Fixed<Size255>.ParseHexStringExact($"{intPart:X}.{frac}");
    }

    [TestMethod]
    public void Karatsuba_SquareOfSchoolbookSqrt2_IsTwo()
    {
        // Compute sqrt(2) via schoolbook multiply (known-correct), round-trip through hex to get
        // the exact bits as a Size255 (Karatsuba) value, then square it with Karatsuba.
        // Result must equal 2 to within 1 LSB (the schoolbook sqrt itself has at most 1-ULP error).
        var sqrt2Hex = new Fixed<Size255Schoolbook>(2).Sqrt().ToHexString();
        var sqrt2    = Fixed<Size255>.ParseHexStringExact(sqrt2Hex);
        var sq       = sqrt2 * sqrt2;
        var two      = new Fixed<Size255>(2);
        var diff     = sq - two;
        // |diff| <= 1 ULP  (the LSB of the fraction part)
        var oneUlp   = Fixed<Size255>.ParseHexStringExact("0." + new string('0', 254 * 8) + "00000001");
        (diff.CompareTo(Fixed<Size255>.Zero) < 0 ? diff.Negate() : diff).CompareTo(oneUlp).Should().BeLessThanOrEqualTo(0);
    }

    [TestMethod]
    public void Karatsuba_IntegerProduct()
    {
        // 3 * 7 = 21 (0x15)
        var result = new Fixed<Size255>(3).Multiply(new Fixed<Size255>(7));
        result.ToHexString().Should().StartWith("15.");
    }

    [TestMethod]
    public void Karatsuba_ByOne_ReturnsOriginal()
    {
        var a = new Fixed<Size255>(42);
        a.Multiply(new Fixed<Size255>(1)).Should().Be(a);
    }

    [TestMethod]
    public void Karatsuba_IsCommutative()
    {
        var a = new Fixed<Size255>(6);
        var b = new Fixed<Size255>(7);
        a.Multiply(b).Should().Be(b.Multiply(a));
    }

    [TestMethod]
    public void Karatsuba_HalfByHalf_ReturnsQuarter()
    {
        // 0.5 * 0.5 = 0.25
        var half    = MakeSize255(0, "80000000", "00000000");
        var quarter = MakeSize255(0, "40000000", "00000000");
        half.Multiply(half).Should().Be(quarter);
    }

    [TestMethod]
    public void Karatsuba_MultiplyByTwo_MatchesDoubleAdd()
    {
        // All fraction bits set → a_lo + a_hi overflows (ca=1) in TruncatedAdd.
        // This exercises the carry-correction overflow path in FullMultiply.
        var a = Fixed<Size255>.ParseHexStringExact("0." + new string('F', 255 * 8));
        (a * new Fixed<Size255>(2)).Should().Be(a + a);
    }

    [TestMethod]
    public void Karatsuba_LowBitsCarryIntoResult()
    {
        // (0.5 + 2^-(255*32))^2 = 0.25 + 2^-(255*32) + subprecision_term.
        // The subprecision term carries into the LSW of the result.
        var a        = MakeSize255(0, "80000000", "00000001");
        var expected = MakeSize255(0, "40000000", "00000001");
        a.Multiply(a).Should().Be(expected);
    }
}

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
