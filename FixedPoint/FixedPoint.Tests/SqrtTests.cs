namespace FixedPoint.Tests;

[TestClass]
public sealed class SqrtTests
{
    [TestMethod]
    public void Sqrt_Zero_IsZero()
    {
        Assert.AreEqual(Fixed.Zero.ToHexString(), Fixed.Zero.Sqrt().ToHexString());
    }

    [TestMethod]
    public void Sqrt_One_IsOne()
    {
        Assert.AreEqual(new Fixed(1).ToHexString(), new Fixed(1).Sqrt().ToHexString());
    }

    [TestMethod]
    public void Sqrt_Four_IsTwo()
    {
        Assert.AreEqual(new Fixed(2).ToHexString(), new Fixed(4).Sqrt().ToHexString());
    }

    [TestMethod]
    public void Sqrt_Quarter_IsHalf()
    {
        // sqrt(0.25) = 0.5
        var quarter = Fixed.ParseHexStringExact("0.40000000000000000000000000000000");
        var half    = Fixed.ParseHexStringExact("0.80000000000000000000000000000000");
        Assert.AreEqual(half.ToHexString(), quarter.Sqrt().ToHexString());
    }

    [TestMethod]
    public void Sqrt_SquaredRoundTrip()
    {
        // sqrt(x*x) matches x to double precision.
        // Note: for non-power-of-2 values Reciprocal() truncates (rounds toward zero),
        // so results can differ by 1 ULP (2^-128) — invisible at double's 52-bit mantissa.
        foreach (var n in new[] { 3, 5, 9, 16 })
        {
            var x = new Fixed(n);
            Assert.AreEqual((double)x, (double)((x * x).Sqrt()), 1e-14, $"Failed for n={n}");
        }
    }

    [TestMethod]
    public void Sqrt_Squared_IsOriginal()
    {
        // sqrt(2)^2 is within 1 ULP (2^-128) of 2 — invisible when cast to double.
        var two = new Fixed(2);
        Assert.AreEqual(2.0, (double)(two.Sqrt() * two.Sqrt()), 1e-14);
    }

    [TestMethod]
    public void Sqrt_MatchesDouble()
    {
        // Verify sqrt(2) matches double to double-precision limits
        Assert.AreEqual(Math.Sqrt(2.0), (double)new Fixed(2).Sqrt(), 1e-15);
    }

    [TestMethod]
    public void Sqrt_Negative_Throws()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => new Fixed(-1).Sqrt());
    }
}
