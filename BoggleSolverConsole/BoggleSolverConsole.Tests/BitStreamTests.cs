using Xunit;

namespace BoggleSolverConsole.Tests;

public class BitStreamTests
{
    [Fact]
    public void WriteBit_ReadBit_Roundtrip()
    {
        using var ms = new MemoryStream();
        var writer = new BitWriter(ms);

        writer.WriteBit(true);
        writer.WriteBit(false);
        writer.WriteBit(true);
        writer.WriteBit(true);
        writer.WriteBit(false);
        writer.WriteBit(false);
        writer.WriteBit(true);
        writer.WriteBit(false);
        writer.Flush();

        ms.Position = 0;
        var reader = new BitReader(ms);

        Assert.True(reader.ReadBit());
        Assert.False(reader.ReadBit());
        Assert.True(reader.ReadBit());
        Assert.True(reader.ReadBit());
        Assert.False(reader.ReadBit());
        Assert.False(reader.ReadBit());
        Assert.True(reader.ReadBit());
        Assert.False(reader.ReadBit());
    }

    [Fact]
    public void WriteBits_ReadBits_Roundtrip()
    {
        using var ms = new MemoryStream();
        var writer = new BitWriter(ms);

        writer.WriteBits(0b101, 3);
        writer.WriteBits(0b11110000, 8);
        writer.WriteBits(0b01, 2);
        writer.Flush();

        ms.Position = 0;
        var reader = new BitReader(ms);

        Assert.Equal(0b101u, reader.ReadBits(3));
        Assert.Equal(0b11110000u, reader.ReadBits(8));
        Assert.Equal(0b01u, reader.ReadBits(2));
    }

    [Fact]
    public void WriteBits_32Bits_Roundtrip()
    {
        using var ms = new MemoryStream();
        var writer = new BitWriter(ms);

        writer.WriteBits(0xDEADBEEF, 32);
        writer.Flush();

        ms.Position = 0;
        var reader = new BitReader(ms);

        Assert.Equal(0xDEADBEEFu, reader.ReadBits(32));
    }

    [Fact]
    public void MixedBitAndMultiBit_Roundtrip()
    {
        using var ms = new MemoryStream();
        var writer = new BitWriter(ms);

        writer.WriteBit(true);       // 1 bit
        writer.WriteBits(0b11, 2);   // 2 bits
        writer.WriteBit(false);      // 1 bit
        writer.WriteBits(0xFF, 8);   // 8 bits
        writer.WriteBit(true);       // 1 bit
        writer.Flush();

        ms.Position = 0;
        var reader = new BitReader(ms);

        Assert.True(reader.ReadBit());
        Assert.Equal(0b11u, reader.ReadBits(2));
        Assert.False(reader.ReadBit());
        Assert.Equal(0xFFu, reader.ReadBits(8));
        Assert.True(reader.ReadBit());
    }

    [Fact]
    public void BitsWritten_TracksCorrectly()
    {
        using var ms = new MemoryStream();
        var writer = new BitWriter(ms);

        Assert.Equal(0, writer.BitsWritten);

        writer.WriteBit(true);
        Assert.Equal(1, writer.BitsWritten);

        writer.WriteBits(0xFF, 8);
        Assert.Equal(9, writer.BitsWritten);

        writer.WriteBits(0xFFFF, 16);
        Assert.Equal(25, writer.BitsWritten);
    }

    [Fact]
    public void BitsRead_TracksCorrectly()
    {
        using var ms = new MemoryStream();
        var writer = new BitWriter(ms);
        writer.WriteBits(0xFFFFFFFF, 32);
        writer.Flush();

        ms.Position = 0;
        var reader = new BitReader(ms);

        Assert.Equal(0, reader.BitsRead);

        reader.ReadBit();
        Assert.Equal(1, reader.BitsRead);

        reader.ReadBits(8);
        Assert.Equal(9, reader.BitsRead);

        reader.ReadBits(16);
        Assert.Equal(25, reader.BitsRead);
    }

    [Fact]
    public void NonByteAligned_Flush_PadsCorrectly()
    {
        using var ms = new MemoryStream();
        var writer = new BitWriter(ms);

        writer.WriteBits(0b101, 3); // Only 3 bits
        writer.Flush();

        Assert.Equal(1, ms.Length); // Should pad to 1 byte

        ms.Position = 0;
        var reader = new BitReader(ms);
        Assert.Equal(0b101u, reader.ReadBits(3));
    }
}
