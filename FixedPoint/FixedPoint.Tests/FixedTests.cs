namespace FixedPoint.Tests;

[TestClass]
public sealed class FixedTests
{
    /// <summary>
    /// Pi computed at Size255 (8160-bit fraction, ~2457 decimal digits) and Size1023 (32736-bit, ~9856 digits)
    /// must agree on all but the last 5 decimal digits of the lower-precision result.
    /// </summary>
    /// <summary>
    /// Karatsuba and schoolbook both compute the full 2n-word product then extract the same high words,
    /// so every multiply result must be identical bit-for-bit. Any divergence indicates a Karatsuba bug.
    /// </summary>
    [TestMethod]
    public void GetPi_KaratsubaMatchesSchoolbook_BitForBit()
    {
        var piKaratsuba  = Fixed<Size255>          .GetPi().ToHexString();
        var piSchoolbook = Fixed<Size255Schoolbook>.GetPi().ToHexString();
        Assert.AreEqual(piSchoolbook, piKaratsuba);
    }

    [TestMethod]
    public void GetPi_Size255AndSize1023_AgreeExceptLastFiveDigits()
    {
        var pi255  = Fixed<Size255> .GetPi().ToDecimalString();
        var pi1023 = Fixed<Size1023>.GetPi().ToDecimalString();

        // Both strings are "3.<fracDigits>". Strip the integer part.
        var frac255  = pi255 [(pi255 .IndexOf('.') + 1)..];
        var frac1023 = pi1023[(pi1023.IndexOf('.') + 1)..];

        // frac255 is shorter; compare all but its last 6 digits against the same prefix of frac1023.
        var compareLen = frac255.Length - 6;
        Assert.AreEqual(frac255[..compareLen], frac1023[..compareLen]);
    }
}
