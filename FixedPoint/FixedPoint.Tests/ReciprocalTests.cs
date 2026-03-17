namespace FixedPoint.Tests;

[TestClass]
public sealed class ReciprocalTests
{
    [TestMethod]
    public void Reciprocal_Two_IsHalf()
    {
        Assert.AreEqual("0.500000000000000000000000000000000000000",
            new Fixed(2).Reciprocal().ToDecimalString());
    }

    [TestMethod]
    public void Reciprocal_Four_IsQuarter()
    {
        Assert.AreEqual("0.250000000000000000000000000000000000000",
            new Fixed(4).Reciprocal().ToDecimalString());
    }

    [TestMethod]
    public void Reciprocal_NegativeTwo_IsNegativeHalf()
    {
        Assert.AreEqual("-0.500000000000000000000000000000000000000",
            new Fixed(-2).Reciprocal().ToDecimalString());
    }

    [TestMethod]
    public void Reciprocal_One_IsOne()
    {
        Assert.AreEqual(new Fixed(1).ToHexString(), new Fixed(1).Reciprocal().ToHexString());
    }

    [TestMethod]
    public void Reciprocal_InvertsMultiply()
    {
        // a * (1/a) is within 1 ULP (2^-128) of 1.0 — invisible at double precision.
        foreach (var n in new[] { 3, 5, 7, 10 })
        {
            var a = new Fixed(n);
            Assert.AreEqual(1.0, (double)(a * a.Reciprocal()), 1e-14, $"Failed for n={n}");
        }
    }

    [TestMethod]
    public void Reciprocal_Zero_Throws()
    {
        Assert.ThrowsExactly<DivideByZeroException>(() => Fixed.Zero.Reciprocal());
    }

    [TestMethod]
    public void Reciprocal_RoundTrip()
    {
        // 1/(1/a) matches a to double precision (sub-ULP error is invisible at 52-bit mantissa)
        var a = new Fixed(7);
        Assert.AreEqual((double)a, (double)(a.Reciprocal().Reciprocal()), 1e-14);
    }
}
