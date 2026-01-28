using BitUtilities;
using Xunit;

namespace FlatTrie.Tests;

public class BitArrayReaderWriterTests
{
    [Fact]
    public void WriteBit_ReadBit_RoundTrip()
    {
        var buffer = new uint[4];
        var writer = new BitArrayWriter(buffer);

        writer.WriteBit(true);
        writer.WriteBit(false);
        writer.WriteBit(true);
        writer.WriteBit(true);
        writer.WriteBit(false);

        var reader = new BitArrayReader(buffer);

        Assert.True(reader.ReadBit());
        Assert.False(reader.ReadBit());
        Assert.True(reader.ReadBit());
        Assert.True(reader.ReadBit());
        Assert.False(reader.ReadBit());
    }

    [Fact]
    public void WriteBits_ReadBits_RoundTrip()
    {
        var buffer = new uint[4];
        var writer = new BitArrayWriter(buffer);

        writer.WriteBits(0b10110, 5);
        writer.WriteBits(0xFF, 8);
        writer.WriteBits(0x12345, 20);

        var reader = new BitArrayReader(buffer);

        Assert.Equal(0b10110u, reader.ReadBits(5));
        Assert.Equal(0xFFu, reader.ReadBits(8));
        Assert.Equal(0x12345u, reader.ReadBits(20));
    }

    [Fact]
    public void WriteBits_CrossesUintBoundary()
    {
        var buffer = new uint[4];
        var writer = new BitArrayWriter(buffer);

        // Write 30 bits, then 10 bits (crosses boundary)
        writer.WriteBits(0x3FFFFFFF, 30);
        writer.WriteBits(0x3FF, 10);

        var reader = new BitArrayReader(buffer);

        Assert.Equal(0x3FFFFFFFu, reader.ReadBits(30));
        Assert.Equal(0x3FFu, reader.ReadBits(10));
    }

    [Fact]
    public void Seek_WritesAtCorrectPosition()
    {
        var buffer = new uint[4];
        var writer = new BitArrayWriter(buffer);

        writer.WriteBits(0xAA, 8);
        writer.Seek(0);
        writer.WriteBits(0x55, 8);

        var reader = new BitArrayReader(buffer);
        Assert.Equal(0x55u, reader.ReadBits(8));
    }

    [Fact]
    public void PeekBit_DoesNotAdvance()
    {
        var buffer = new uint[4];
        buffer[0] = 0b10101;

        var reader = new BitArrayReader(buffer);

        Assert.True(reader.PeekBit());
        Assert.Equal(0, reader.BitPosition);
        Assert.True(reader.ReadBit());
        Assert.Equal(1, reader.BitPosition);
    }

    [Fact]
    public void Skip_AdvancesPosition()
    {
        var buffer = new uint[4];
        buffer[0] = 0xFFFFFFFF;

        var reader = new BitArrayReader(buffer);

        reader.Skip(10);
        Assert.Equal(10, reader.BitPosition);

        reader.Skip(22);
        Assert.Equal(32, reader.BitPosition);
    }

    [Fact]
    public void WritePrefix_ReadsBackCorrectly()
    {
        var buffer = new uint[4];
        var writer = new BitArrayWriter(buffer);

        var prefix = BitPrefix.FromBools(true, false, true, true, false, true);
        writer.WritePrefix(prefix);

        var reader = new BitArrayReader(buffer);
        var readPrefix = reader.ReadPrefix(6);

        Assert.Equal(prefix.Length, readPrefix.Length);
        Assert.Equal(prefix.Bits, readPrefix.Bits);
    }

    [Fact]
    public void Full32BitWrite_Works()
    {
        var buffer = new uint[4];
        var writer = new BitArrayWriter(buffer);

        writer.WriteBits(0xDEADBEEF, 32);

        var reader = new BitArrayReader(buffer);
        Assert.Equal(0xDEADBEEFu, reader.ReadBits(32));
    }

    [Fact]
    public void MultipleFullWrites_Works()
    {
        var buffer = new uint[4];
        var writer = new BitArrayWriter(buffer);

        writer.WriteBits(0x11111111, 32);
        writer.WriteBits(0x22222222, 32);
        writer.WriteBits(0x33333333, 32);
        writer.WriteBits(0x44444444, 32);

        var reader = new BitArrayReader(buffer);
        Assert.Equal(0x11111111u, reader.ReadBits(32));
        Assert.Equal(0x22222222u, reader.ReadBits(32));
        Assert.Equal(0x33333333u, reader.ReadBits(32));
        Assert.Equal(0x44444444u, reader.ReadBits(32));
    }

    [Fact]
    public void OverwriteBits_ClearsOldBits()
    {
        var buffer = new uint[4];

        // Write all 1s
        var writer = new BitArrayWriter(buffer);
        writer.WriteBits(0xFF, 8);

        // Overwrite with 0s
        writer.Seek(0);
        writer.WriteBits(0x00, 8);

        var reader = new BitArrayReader(buffer);
        Assert.Equal(0u, reader.ReadBits(8));
    }
}
