namespace FixedPoint;

public readonly partial struct Fixed
{
    /// <summary>
    /// Returns sqrt(a) using Heron's method: x = (x + a * x.Reciprocal()) >> 1.
    /// Seeds from the double sqrt (~53 bits); ReciprocalIterations steps reach full precision.
    /// </summary>
    public Fixed Sqrt()
    {
        var cmp = CompareTo(Zero);
        if (cmp < 0)
            throw new InvalidOperationException( "Cannot take the square root of a negative number.");
        if (cmp == 0)
            return Zero;

        var x = (Fixed)Math.Sqrt((double)this);
        for (var i = 0; i < ReciprocalIterations; i++)
            x = (x + this * x.Reciprocal()) >> 1;

        return x;
    }
}
