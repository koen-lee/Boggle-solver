using BitUtilities;
using Xunit;

namespace BitUtilities.Tests;

public class BitArrayWriterTests
{
    [Fact]
    public void WriteBit_SingleBit_SetsCorrectly()
    {
        var buffer = new uint[1];
        var writer = new BitArrayWriter(buffer);

        writer.WriteBit(true);

        Assert.Equal(1u, buffer[0]);
    }

    [Fact]
    public void WriteBits_MultipleValues_WritesCorrectly()
    {
        var buffer = new uint[1];
        var writer = new BitArrayWriter(buffer);

        writer.WriteBits(0b1010, 4);  // bits 0-3
        writer.WriteBits(0b1100, 4);  // bits 4-7

        Assert.Equal(0b11001010u, buffer[0] & 0xFF);
    }

    [Fact]
    public void WriteBits_CrossWordBoundary_WritesCorrectly()
    {
        var buffer = new uint[2];
        var writer = new BitArrayWriter(buffer, 30);  // Start near end of first word

        writer.WriteBits(0b1111, 4);  // 2 bits in word 0, 2 bits in word 1

        Assert.Equal(0b11u << 30, buffer[0]);
        Assert.Equal(0b11u, buffer[1]);
    }

    [Fact]
    public void ShiftBitsLeft_WordAligned_PreservesPrecedingBits()
    {
        // Setup: [0-31: data A] [32-63: data B] [64-95: data C]
        var buffer = new uint[4];
        buffer[0] = 0xAAAAAAAA;  // Data A - should be preserved
        buffer[1] = 0xBBBBBBBB;  // Data B - should be preserved
        buffer[2] = 0xCCCCCCCC;  // Data C - will be shifted
        buffer[3] = 0xDDDDDDDD;  // Data D - will be shifted

        // Shift bits [64, 128) left by 32 bits to [32, 96)
        BitArrayWriter.ShiftBitsLeft(buffer, fromBitPos: 64, bitsToMove: 64, delta: 32);

        Assert.Equal(0xAAAAAAAAu, buffer[0]);  // Preserved
        Assert.Equal(0xCCCCCCCCu, buffer[1]);  // Was at [64-95], now at [32-63]
        Assert.Equal(0xDDDDDDDDu, buffer[2]);  // Was at [96-127], now at [64-95]
    }

    [Fact]
    public void ShiftBitsLeft_NonAligned_PreservesPrecedingBits()
    {
        // Setup: bits [0-69] = prefix data, bits [70-133] = value, bits [134-199] = children
        // This mimics the FlatTrie delete scenario
        var buffer = new uint[8];

        // Set up some recognizable patterns
        // Bits 0-69 (prefix area) - set to pattern 0xAA
        var writer = new BitArrayWriter(buffer, 0);
        for (int i = 0; i < 70; i++)
            writer.WriteBit((i & 1) == 0);  // Alternating pattern

        // Bits 70-133 (value area - 64 bits) - all 1s
        for (int i = 0; i < 64; i++)
            writer.WriteBit(true);

        // Bits 134-199 (children area - 66 bits) - recognizable pattern 0x55
        for (int i = 0; i < 66; i++)
            writer.WriteBit((i & 1) == 1);

        // Read back original values to verify setup
        var originalPrefix = ReadBits(buffer, 0, 70);
        var originalChildren = ReadBits(buffer, 134, 66);

        // Shift children [134, 200) left by 64 bits to [70, 136)
        BitArrayWriter.ShiftBitsLeft(buffer, fromBitPos: 134, bitsToMove: 66, delta: 64);

        // Verify prefix bits [0-69] are preserved
        var newPrefix = ReadBits(buffer, 0, 70);
        Assert.Equal(originalPrefix, newPrefix);

        // Verify children are now at [70-135]
        var shiftedChildren = ReadBits(buffer, 70, 66);
        Assert.Equal(originalChildren, shiftedChildren);
    }

    [Fact]
    public void ShiftBitsRight_WordAligned_MovesCorrectly()
    {
        var buffer = new uint[4];
        buffer[0] = 0xAAAAAAAA;  // Will be preserved
        buffer[1] = 0xBBBBBBBB;  // Will be shifted right

        // Shift bits [32, 64) right by 32 bits to [64, 96)
        BitArrayWriter.ShiftBitsRight(buffer, fromBitPos: 32, bitsToMove: 32, delta: 32);

        Assert.Equal(0xAAAAAAAAu, buffer[0]);  // Preserved
        // buffer[1] is the gap - don't care about its value
        Assert.Equal(0xBBBBBBBBu, buffer[2]);  // Shifted from [32-63] to [64-95]
    }

    [Fact]
    public void ShiftBitsRight_NonAligned_MovesCorrectly()
    {
        var buffer = new uint[8];

        // Set up recognizable data at bits [100-163] (64 bits)
        var writer = new BitArrayWriter(buffer, 100);
        writer.WriteBits(0xDEADBEEF, 32);
        writer.WriteBits(0xCAFEBABE, 32);

        // Shift bits [100, 164) right by 50 bits to [150, 214)
        BitArrayWriter.ShiftBitsRight(buffer, fromBitPos: 100, bitsToMove: 64, delta: 50);

        // Read back from new position
        var reader = new BitArrayReader(buffer, 150);
        Assert.Equal(0xDEADBEEFu, reader.ReadBits(32));
        Assert.Equal(0xCAFEBABEu, reader.ReadBits(32));
    }

    [Fact]
    public void ShiftBitsLeft_ZeroDelta_NoOp()
    {
        var buffer = new uint[2];
        buffer[0] = 0xAAAAAAAA;
        buffer[1] = 0xBBBBBBBB;

        BitArrayWriter.ShiftBitsLeft(buffer, fromBitPos: 32, bitsToMove: 32, delta: 0);

        Assert.Equal(0xAAAAAAAAu, buffer[0]);
        Assert.Equal(0xBBBBBBBBu, buffer[1]);
    }

    [Fact]
    public void ShiftBitsRight_ZeroBitsToMove_NoOp()
    {
        var buffer = new uint[2];
        buffer[0] = 0xAAAAAAAA;
        buffer[1] = 0xBBBBBBBB;

        BitArrayWriter.ShiftBitsRight(buffer, fromBitPos: 32, bitsToMove: 0, delta: 32);

        Assert.Equal(0xAAAAAAAAu, buffer[0]);
        Assert.Equal(0xBBBBBBBBu, buffer[1]);
    }

    [Fact]
    public void ShiftBitsLeft_SmallShift_PreservesPartialWord()
    {
        var buffer = new uint[2];

        // Write pattern: first 10 bits = alternating, next 22 bits = all 1s
        var writer = new BitArrayWriter(buffer, 0);
        for (int i = 0; i < 10; i++)
            writer.WriteBit((i & 1) == 0);
        for (int i = 0; i < 22; i++)
            writer.WriteBit(true);

        var originalFirst10 = ReadBits(buffer, 0, 10);
        var originalNext22 = ReadBits(buffer, 10, 22);

        // Shift bits [10, 32) left by 5 bits to [5, 27)
        BitArrayWriter.ShiftBitsLeft(buffer, fromBitPos: 10, bitsToMove: 22, delta: 5);

        // First 5 bits should be preserved
        var first5 = ReadBits(buffer, 0, 5);
        Assert.Equal(originalFirst10.Substring(0, 5), first5);

        // Bits [5, 27) should now contain what was at [10, 32)
        var shifted = ReadBits(buffer, 5, 22);
        Assert.Equal(originalNext22, shifted);
    }

    /// <summary>
    /// Helper to read bits as a string of 0s and 1s for easy comparison.
    /// </summary>
    private static string ReadBits(uint[] buffer, int startBit, int count)
    {
        var reader = new BitArrayReader(buffer, startBit);
        var chars = new char[count];
        for (int i = 0; i < count; i++)
            chars[i] = reader.ReadBit() ? '1' : '0';
        return new string(chars);
    }
}
