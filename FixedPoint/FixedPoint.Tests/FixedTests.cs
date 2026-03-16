namespace FixedPoint.Tests;

[TestClass]
public sealed class FixedTests
{
    // Helpers
    private static string ZeroFraction => new string('0', 32);

    [TestMethod]
    public void Zero_HasZeroIntegerAndFraction()
    {
        Assert.AreEqual($"0.{ZeroFraction}", Fixed.Zero.ToString());
    }

    [TestMethod]
    public void Constructor_PositiveInteger_SetsIntegerPart()
    {
        Assert.AreEqual($"5.{ZeroFraction}", new Fixed(5).ToString());
    }

    [TestMethod]
    public void Constructor_NegativeInteger_ShowsTwosComplement()
    {
        // -1 in hex two's complement is FFFFFFFF
        Assert.AreEqual($"FFFFFFFF.{ZeroFraction}", new Fixed(-1).ToString());
    }

    [TestMethod]
    public void Constructor_Zero_MatchesZeroConstant()
    {
        Assert.AreEqual(Fixed.Zero.ToString(), new Fixed(0).ToString());
    }

    [TestMethod]
    public void ToString_LargePositiveInteger_FormatsAsHex()
    {
        Assert.AreEqual($"10.{ZeroFraction}", new Fixed(16).ToString());
    }

    // --- Add ---

    [TestMethod]
    public void Add_TwoPositiveIntegers()
    {
        var result = new Fixed(3).Add(new Fixed(5));
        Assert.AreEqual($"8.{ZeroFraction}", result.ToString());
    }

    [TestMethod]
    public void Add_WithZero_ReturnsOriginal()
    {
        var a = new Fixed(7);
        Assert.AreEqual(a.ToString(), a.Add(Fixed.Zero).ToString());
    }

    [TestMethod]
    public void Add_Zero_WithZero_ReturnsZero()
    {
        Assert.AreEqual(Fixed.Zero.ToString(), Fixed.Zero.Add(Fixed.Zero).ToString());
    }

    [TestMethod]
    public void Add_PositiveAndNegative_CancelOut()
    {
        var result = new Fixed(3).Add(new Fixed(-3));
        Assert.AreEqual($"0.{ZeroFraction}", result.ToString());
    }

    [TestMethod]
    public void Add_IsCommutative()
    {
        var a = new Fixed(4);
        var b = new Fixed(9);
        Assert.AreEqual(a.Add(b).ToString(), b.Add(a).ToString());
    }

    // --- Subtract ---

    [TestMethod]
    public void Subtract_PositiveIntegers()
    {
        var result = new Fixed(10).Subtract(new Fixed(3));
        Assert.AreEqual($"7.{ZeroFraction}", result.ToString());
    }

    [TestMethod]
    public void Subtract_SameValue_ReturnsZero()
    {
        var a = new Fixed(5);
        Assert.AreEqual($"0.{ZeroFraction}", a.Subtract(a).ToString());
    }

    [TestMethod]
    public void Subtract_Zero_ReturnsOriginal()
    {
        var a = new Fixed(6);
        Assert.AreEqual(a.ToString(), a.Subtract(Fixed.Zero).ToString());
    }

    [TestMethod]
    public void Subtract_ProducingNegativeResult()
    {
        // 2 - 3 = -1
        var result = new Fixed(2).Subtract(new Fixed(3));
        Assert.AreEqual($"FFFFFFFF.{ZeroFraction}", result.ToString());
    }

    // --- ShiftRight ---

    [TestMethod]
    public void ShiftRight_ByZero_ReturnsSame()
    {
        var a = new Fixed(8);
        Assert.AreEqual(a.ToString(), a.ShiftRight(0).ToString());
    }

    [TestMethod]
    public void ShiftRight_By32_ThrowsNotImplementedException()
    {
        Assert.ThrowsException<NotImplementedException>(() => new Fixed(1).ShiftRight(32));
    }

    [TestMethod]
    public void ShiftRight_By33_ThrowsNotImplementedException()
    {
        Assert.ThrowsException<NotImplementedException>(() => new Fixed(1).ShiftRight(33));
    }

    [TestMethod]
    public void ShiftRight_IntegerByOne_HalvesIntegerPart()
    {
        // integer: 8 >> 1 = 4
        var result = new Fixed(8).ShiftRight(1);
        Assert.AreEqual(4, GetIntegerPart(result));
    }

    [TestMethod]
    public void ShiftRight_IntegerByTwo_QuartersIntegerPart()
    {
        // integer: 8 >> 2 = 2
        var result = new Fixed(8).ShiftRight(2);
        Assert.AreEqual(2, GetIntegerPart(result));
    }

    [TestMethod]
    public void ShiftRight_NegativeInteger_ArithmeticShift()
    {
        // -4 >> 1 = -2 (arithmetic shift preserves sign)
        var result = new Fixed(-4).ShiftRight(1);
        Assert.AreEqual(-2, GetIntegerPart(result));
    }

    [TestMethod]
    public void ShiftRight_NegativeByOne_IsArithmetic()
    {
        // Verify sign extension: -1 >> 1 = -1
        var result = new Fixed(-1).ShiftRight(1);
        Assert.AreEqual(-1, GetIntegerPart(result));
    }

    // --- Round-trip consistency ---

    [TestMethod]
    public void AddThenSubtract_ReturnOriginal()
    {
        var a = new Fixed(10);
        var b = new Fixed(3);
        Assert.AreEqual(a.ToString(), a.Add(b).Subtract(b).ToString());
    }

    // Helper: extract the integer part from ToString() by reading up to the '.'
    private static int GetIntegerPart(Fixed f)
    {
        var s = f.ToString();
        var hexPart = s[..s.IndexOf('.')];
        return (int)Convert.ToUInt32(hexPart, 16);
    }
}
