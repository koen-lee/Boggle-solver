using BitUtilities;
using Xunit;

namespace FlatTrie.Tests;

public class VarIntTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 1)]
    [InlineData(4, 2)]
    [InlineData(255, 2)]
    [InlineData(256, 3)]
    [InlineData(262143, 3)]
    public void GetClass_ReturnsCorrectClass(int value, int expectedClass)
    {
        Assert.Equal(expectedClass, VarInt.GetClass(value));
    }

    [Theory]
    [InlineData(0, 2)]   // Class 0: 2 bits total
    [InlineData(1, 4)]   // Class 1: 2 + 2 bits
    [InlineData(3, 4)]
    [InlineData(4, 10)]  // Class 2: 2 + 8 bits
    [InlineData(255, 10)]
    [InlineData(256, 20)] // Class 3: 2 + 18 bits
    public void GetEncodedBitCount_ReturnsCorrectCount(int value, int expectedBits)
    {
        Assert.Equal(expectedBits, VarInt.GetEncodedBitCount(value));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(100)]
    [InlineData(255)]
    [InlineData(256)]
    [InlineData(1000)]
    [InlineData(262143)]
    public void WriteRead_RoundTrip(int value)
    {
        var buffer = new uint[4];
        var writer = new BitArrayWriter(buffer);

        VarInt.Write(ref writer, value);

        var reader = new BitArrayReader(buffer);
        int result = VarInt.Read(ref reader);

        Assert.Equal(value, result);
        Assert.Equal(writer.BitPosition, reader.BitPosition);
    }

    [Fact]
    public void MultipleValues_RoundTrip()
    {
        var buffer = new uint[8];
        var writer = new BitArrayWriter(buffer);

        int[] values = [0, 1, 3, 4, 100, 255, 256, 1000];

        foreach (var v in values)
        {
            VarInt.Write(ref writer, v);
        }

        var reader = new BitArrayReader(buffer);

        foreach (var expected in values)
        {
            int actual = VarInt.Read(ref reader);
            Assert.Equal(expected, actual);
        }
    }
}
