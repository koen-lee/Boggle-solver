using BitUtilities;
using Xunit;

namespace BitUtilities.Tests;

public class BitStringTests
{
    [Fact]
    public void Empty_HasZeroLength()
    {
        var bits = BitString.Empty;

        Assert.Equal(0, bits.Length);
        Assert.True(bits.IsEmpty);
    }

    [Fact]
    public void FromBytes_StoresBitsCorrectly()
    {
        // 0b10101010 = bits: 0,1,0,1,0,1,0,1 (LSB-first)
        var bits = BitString.FromBytes([0b10101010]);

        Assert.Equal(8, bits.Length);
        Assert.False(bits[0]); // LSB
        Assert.True(bits[1]);
        Assert.False(bits[2]);
        Assert.True(bits[3]);
        Assert.False(bits[4]);
        Assert.True(bits[5]);
        Assert.False(bits[6]);
        Assert.True(bits[7]); // MSB
    }

    [Fact]
    public void FromBytes_MultipleBytes_StoresCorrectly()
    {
        var bits = BitString.FromBytes([0xFF, 0x00]);

        Assert.Equal(16, bits.Length);
        // First byte: all 1s
        for (int i = 0; i < 8; i++)
            Assert.True(bits[i], $"Bit {i} should be true");
        // Second byte: all 0s
        for (int i = 8; i < 16; i++)
            Assert.False(bits[i], $"Bit {i} should be false");
    }

    [Fact]
    public void FromInts_StoresBitsCorrectly()
    {
        var bits = BitString.FromInts([0b1101], 4);

        Assert.Equal(4, bits.Length);
        Assert.True(bits[0]);
        Assert.False(bits[1]);
        Assert.True(bits[2]);
        Assert.True(bits[3]);
    }

    [Fact]
    public void Slice_ReturnsViewOfSameData()
    {
        var bits = BitString.FromBytes([0xFF, 0x00]);
        var slice = bits.Slice(4, 8);

        Assert.Equal(8, slice.Length);
        // Bits 4-7 are 1s (from 0xFF), bits 8-11 are 0s (from 0x00)
        Assert.True(slice[0]);  // Was bit 4
        Assert.True(slice[1]);  // Was bit 5
        Assert.True(slice[2]);  // Was bit 6
        Assert.True(slice[3]);  // Was bit 7
        Assert.False(slice[4]); // Was bit 8
        Assert.False(slice[5]); // Was bit 9
        Assert.False(slice[6]); // Was bit 10
        Assert.False(slice[7]); // Was bit 11
    }

    [Fact]
    public void Slice_ZeroLength_ReturnsEmpty()
    {
        var bits = BitString.FromBytes([0xFF]);
        var slice = bits.Slice(4, 0);

        Assert.Equal(0, slice.Length);
        Assert.True(slice.IsEmpty);
    }

    [Fact]
    public void Slice_ExceedsBounds_Throws()
    {
        var bits = BitString.FromBytes([0xFF]);

        Assert.Throws<ArgumentOutOfRangeException>(() => bits.Slice(4, 8));
    }

    [Fact]
    public void ToBitPrefix_ExtractsCorrectBits()
    {
        // 0b11011010 = bits: 0,1,0,1,1,0,1,1 (LSB-first)
        var bits = BitString.FromBytes([0b11011010]);
        var prefix = bits.ToBitPrefix(2, 4);

        Assert.Equal(4, prefix.Length);
        // Bits 2-5 of 0b11011010 (LSB-first: 0,1,0,1,1,0,1,1)
        // bits[2]=0, bits[3]=1, bits[4]=1, bits[5]=0
        Assert.False(prefix[0]);
        Assert.True(prefix[1]);
        Assert.True(prefix[2]);
        Assert.False(prefix[3]);
    }

    [Fact]
    public void ToBitPrefix_EntireBitString_Works()
    {
        var bits = BitString.FromBytes([0xAB]);
        var prefix = bits.ToBitPrefix(0, bits.Length);

        Assert.Equal(8, prefix.Length);
        Assert.Equal(0xABu, prefix.Bits);
    }

    [Fact]
    public void ToBitPrefix_SpanningTwoUints_ExtractsCorrectly()
    {
        // Create 64 bits spanning two uints
        var bits = BitString.FromBytes([0xFF, 0xFF, 0xFF, 0xFF, 0xAA, 0xAA, 0xAA, 0xAA]);

        // Extract bits crossing the uint boundary (bits 28-35)
        var prefix = bits.ToBitPrefix(28, 8);

        Assert.Equal(8, prefix.Length);
        // Bits 28-31 from first uint (all 1s = 0xF), bits 32-35 from second (0xAA pattern = 0xA)
        // Combined: 0xAF (with bits 28-31 at low positions, bits 32-35 at high)
        Assert.Equal(0xAFu, prefix.Bits);
    }

    [Fact]
    public void SlicedBitString_ToBitPrefix_UsesCorrectOffset()
    {
        var bits = BitString.FromBytes([0x00, 0xFF]);
        var slice = bits.Slice(8, 8);
        var prefix = slice.ToBitPrefix(0, slice.Length);

        Assert.Equal(8, prefix.Length);
        Assert.Equal(0xFFu, prefix.Bits);
    }

    [Fact]
    public void SlicedBitString_Indexer_UsesCorrectOffset()
    {
        var bits = BitString.FromBytes([0x00, 0xFF]);
        var slice = bits.Slice(8, 8);

        // All bits in the slice (which is the second byte) should be 1
        for (int i = 0; i < 8; i++)
            Assert.True(slice[i], $"Slice bit {i} should be true");
    }

    [Fact]
    public void FromBools_StoresBitsCorrectly()
    {
        var bools = new List<bool> { true, false, true, true, false };
        var bits = BitString.FromBools(bools);

        Assert.Equal(5, bits.Length);
        Assert.True(bits[0]);
        Assert.False(bits[1]);
        Assert.True(bits[2]);
        Assert.True(bits[3]);
        Assert.False(bits[4]);
    }

    [Fact]
    public void FromBools_Empty_ReturnsEmpty()
    {
        var bits = BitString.FromBools(new List<bool>());

        Assert.True(bits.IsEmpty);
        Assert.Equal(0, bits.Length);
    }

    [Fact]
    public void ToBitPrefix_ZeroLength_ReturnsEmpty()
    {
        var bits = BitString.FromBytes([0xFF]);
        var prefix = bits.ToBitPrefix(0, 0);

        Assert.Equal(0, prefix.Length);
    }

    [Fact]
    public void ToBitPrefix_ExceedsMaxLength_Throws()
    {
        // Create 64 bits
        var bits = BitString.FromBytes([0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF]);

        Assert.Throws<ArgumentOutOfRangeException>(() => bits.ToBitPrefix(0, 33));
    }

    [Fact]
    public void CommonPrefixLength_IdenticalBits_ReturnsFullLength()
    {
        var bits = BitString.FromBytes([0xAB, 0xCD]);
        var a = bits.AsReadOnly();
        var b = bits.AsReadOnly();

        Assert.Equal(16, a.CommonPrefixLength(ref b));
    }

    [Fact]
    public void CommonPrefixLength_CompletelyDifferent_ReturnsZero()
    {
        var a = BitString.FromBytes([0x00]).AsReadOnly();
        var b = BitString.FromBytes([0x01]).AsReadOnly(); // Differs at bit 0

        Assert.Equal(0, a.CommonPrefixLength(ref b));
    }

    [Fact]
    public void CommonPrefixLength_DifferAtBit4_Returns4()
    {
        // 0x0F = 0b00001111 (bits 0-3 are 1, bits 4-7 are 0)
        // 0x1F = 0b00011111 (bits 0-4 are 1, bit 5 is 0, etc)
        var a = BitString.FromBytes([0x0F]).AsReadOnly();
        var b = BitString.FromBytes([0x1F]).AsReadOnly();

        // Differ at bit 4 (first has 0, second has 1)
        Assert.Equal(4, a.CommonPrefixLength(ref b));
    }

    [Fact]
    public void CommonPrefixLength_DifferentLengths_ReturnsMinLength()
    {
        var short_ = BitString.FromBytes([0xFF]).AsReadOnly();
        var long_ = BitString.FromBytes([0xFF, 0xFF]).AsReadOnly();

        Assert.Equal(8, short_.CommonPrefixLength(ref long_));
        Assert.Equal(8, long_.CommonPrefixLength(ref short_));
    }

    [Fact]
    public void CommonPrefixLength_SpanningMultipleUints_Works()
    {
        // 8 bytes = 64 bits = 2 uints
        var a = BitString.FromBytes([0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00]).AsReadOnly();
        var b = BitString.FromBytes([0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00]).AsReadOnly();

        // First 32 bits (first uint) match, then differ at bit 32
        Assert.Equal(32, a.CommonPrefixLength(ref b));
    }

    [Fact]
    public void CommonPrefixLength_WithSlices_Works()
    {
        var a = BitString.FromBytes([0x00, 0xFF]).Slice(8, 8).AsReadOnly();
        var b = BitString.FromBytes([0xFF]).AsReadOnly();

        Assert.Equal(8, a.CommonPrefixLength(ref b));
    }

    [Fact]
    public void CommonPrefixLength_EmptyStrings_ReturnsZero()
    {
        var a = ReadOnlyBitString.Empty;
        var b = BitString.FromBytes([0xFF]).AsReadOnly();

        Assert.Equal(0, a.CommonPrefixLength(ref b));
    }

    [Fact]
    public void CommonPrefixLength_LongStrings_Works()
    {
        // 32 bytes = 256 bits = 8 uints - all matching
        var bytes = new byte[32];
        Array.Fill(bytes, (byte)0xAA);
        var a = BitString.FromBytes(bytes).AsReadOnly();
        var b = BitString.FromBytes(bytes).AsReadOnly();

        Assert.Equal(256, a.CommonPrefixLength(ref b));
    }

    [Fact]
    public void CommonPrefixLength_DifferenceInLastPartialChunk_Works()
    {
        // 5 bytes = 40 bits, differ at bit 36
        var a = BitString.FromBytes([0xFF, 0xFF, 0xFF, 0xFF, 0x0F]).AsReadOnly();
        var b = BitString.FromBytes([0xFF, 0xFF, 0xFF, 0xFF, 0x1F]).AsReadOnly();

        // Bits 32-35 are the same (0xF), bit 36 differs
        Assert.Equal(36, a.CommonPrefixLength(ref b));
    }
}
