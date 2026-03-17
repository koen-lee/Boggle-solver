namespace FixedPoint;

public readonly partial struct Fixed<TSize> where TSize : struct, IFixedSize
{
    private static readonly Fixed<TSize> One = new Fixed<TSize>(1);

    /// <summary>
    /// Returns sqrt(a) using Heron's method: x = (x + a * x.Reciprocal()) >> 1.
    /// Seeds from the double sqrt (~53 bits); ReciprocalIterations steps reach full precision.
    /// </summary>
    public Fixed<TSize> Sqrt()
    {
        var cmp = CompareTo(Zero);
        if (cmp < 0)
            throw new InvalidOperationException("Cannot take the square root of a negative number.");
        if (cmp == 0)
            return Zero;

        var x = (Fixed<TSize>)Math.Sqrt((double)this);
        for (var i = 0; i < ReciprocalIterations; i++)
            x = (x + this * x.Reciprocal()) >> 1;

        return x;
    }

    /// <summary>
    /// Returns an approximation of π, computed using the Gauss-Legendre algorithm.
    /// </summary>
    /// <returns></returns>
    public static Fixed<TSize> GetPi()
    {
        var x1 = new Fixed<TSize>(2).Sqrt();
        var x2 = One;
        var S = Zero;
        var c = One;
        int k = 0;
        while (c != Zero)
        {
            S += c << k - 1;
            var aMean = (x1 + x2) >> 1;
            var gMean = (x1 * x2).Sqrt();
            x1 = aMean;
            x2 = gMean;
            c = (x1 + x2) * (x1 - x2);
            k++;
        }
        var pi = x1 * x1 / (One - S);
        return pi;
    }
}
