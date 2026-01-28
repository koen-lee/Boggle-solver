using System.Collections;
using BitUtilities;
using Xunit;

namespace BitUtilities.Tests;

public class BitPrefixTests
{
    [Fact]
    public void Empty_HasZeroLength()
    {
        var prefix = BitPrefix.Empty;

        Assert.Equal(0, prefix.Length);
    }

    [Fact]
    public void FromBitArray_CopiesBitsCorrectly()
    {
        var bits = new BitArray([true, false, true, true, false]);
        var prefix = BitPrefix.FromBitArray(bits, 0, 5);

        Assert.Equal(5, prefix.Length);
        Assert.True(prefix[0]);
        Assert.False(prefix[1]);
        Assert.True(prefix[2]);
        Assert.True(prefix[3]);
        Assert.False(prefix[4]);
    }

    [Fact]
    public void FromBitArray_WithOffset_CopiesCorrectSlice()
    {
        var bits = new BitArray([true, false, true, true, false]);
        var prefix = BitPrefix.FromBitArray(bits, 2, 3);

        Assert.Equal(3, prefix.Length);
        Assert.True(prefix[0]);  // was bits[2]
        Assert.True(prefix[1]);  // was bits[3]
        Assert.False(prefix[2]); // was bits[4]
    }

    [Fact]
    public void FromBitArray_ZeroLength_ReturnsEmpty()
    {
        var bits = new BitArray([true, false, true]);
        var prefix = BitPrefix.FromBitArray(bits, 1, 0);

        Assert.Equal(0, prefix.Length);
    }

    [Fact]
    public void FromBitArray_ExceedsMaxLength_Throws()
    {
        var bits = new BitArray(30);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BitPrefix.FromBitArray(bits, 0, BitPrefix.MaxLength + 1));
    }

    [Fact]
    public void Slice_ReturnsCorrectSubset()
    {
        var bits = new BitArray([true, false, true, true, false]);
        var prefix = BitPrefix.FromBitArray(bits, 0, 5);

        var slice = prefix.Slice(1, 3);

        Assert.Equal(3, slice.Length);
        Assert.False(slice[0]); // was prefix[1]
        Assert.True(slice[1]);  // was prefix[2]
        Assert.True(slice[2]);  // was prefix[3]
    }

    [Fact]
    public void Slice_FromStart_ReturnsPrefix()
    {
        var bits = new BitArray([true, false, true, true, false]);
        var prefix = BitPrefix.FromBitArray(bits, 0, 5);

        var slice = prefix.Slice(0, 3);

        Assert.Equal(3, slice.Length);
        Assert.True(slice[0]);
        Assert.False(slice[1]);
        Assert.True(slice[2]);
    }

    [Fact]
    public void Slice_ZeroLength_ReturnsEmpty()
    {
        var prefix = BitPrefix.FromBools(true, false, true);

        var slice = prefix.Slice(1, 0);

        Assert.Equal(0, slice.Length);
    }

    [Fact]
    public void Slice_ExceedsBounds_Throws()
    {
        var prefix = BitPrefix.FromBools([true, false, true]);

        Assert.Throws<ArgumentOutOfRangeException>(() => prefix.Slice(1, 3));
    }

    [Fact]
    public void Append_SingleBit_IncreasesLength()
    {
        var prefix = BitPrefix.Empty;

        prefix = prefix.Append(true);

        Assert.Equal(1, prefix.Length);
        Assert.True(prefix[0]);
    }

    [Fact]
    public void Append_MultipleBits_PreservesOrder()
    {
        var prefix = BitPrefix.Empty
            .Append(true)
            .Append(false)
            .Append(true);

        Assert.Equal(3, prefix.Length);
        Assert.True(prefix[0]);
        Assert.False(prefix[1]);
        Assert.True(prefix[2]);
    }

    [Fact]
    public void Append_MultipleBits_array_PreservesOrder()
    {
        var prefix = BitPrefix.Empty.Append([true, false, true, true]);

        Assert.Equal(4, prefix.Length);
        Assert.True(prefix[0]);
        Assert.False(prefix[1]);
        Assert.True(prefix[2]);
        Assert.True(prefix[3]);
    }

    [Fact]
    public void Create_array_PreservesOrder()
    {
        var prefix = BitPrefix.FromBools(true, false, true, true);

        Assert.Equal(4, prefix.Length);
        Assert.True(prefix[0]);
        Assert.False(prefix[1]);
        Assert.True(prefix[2]);
        Assert.True(prefix[3]);
    }

    [Fact]
    public void Append_AtMaxLength_Throws()
    {
        var bits = new BitArray(BitPrefix.MaxLength);
        var prefix = BitPrefix.FromBitArray(bits, 0, BitPrefix.MaxLength);

        Assert.Throws<InvalidOperationException>(() => prefix.Append(true));
    }

    [Fact]
    public void Append_array_AtMaxLength_Throws()
    {
        var bits = new BitArray(BitPrefix.MaxLength);
        var prefix = BitPrefix.FromBitArray(bits, 0, BitPrefix.MaxLength);

        Assert.Throws<InvalidOperationException>(() => prefix.Append([true]));
    }

    [Fact]
    public void Append_BitPrefix_CombinesPrefixes()
    {
        var prefix1 = BitPrefix.Empty.Append(true).Append(false);
        var prefix2 = BitPrefix.Empty.Append(true).Append(true);

        var combined = prefix1.Append(prefix2);

        Assert.Equal(4, combined.Length);
        Assert.True(combined[0]);
        Assert.False(combined[1]);
        Assert.True(combined[2]);
        Assert.True(combined[3]);
    }

    [Fact]
    public void Append_BitPrefix_ExceedsMaxLength_Throws()
    {
        var bits = new BitArray(BitPrefix.MaxLength);
        var prefix1 = BitPrefix.FromBitArray(bits, 0, BitPrefix.MaxLength);
        var prefix2 = BitPrefix.Empty.Append(true);

        Assert.Throws<InvalidOperationException>(() => prefix1.Append(prefix2));
    }

    [Fact]
    public void Append_EmptyBitPrefix_ReturnsOriginal()
    {
        var prefix = BitPrefix.Empty.Append(true).Append(false);

        var result = prefix.Append(BitPrefix.Empty);

        Assert.Equal(2, result.Length);
        Assert.True(result[0]);
        Assert.False(result[1]);
    }

    [Fact]
    public void MaxLength_Is32()
    {
        Assert.Equal(32, BitPrefix.MaxLength);
    }

    [Fact]
    public void Indexer_ReturnsCorrectBits()
    {
        // Create a prefix with alternating bits
        var prefix = BitPrefix.Empty;
        for (int i = 0; i < 8; i++)
        {
            prefix = prefix.Append(i % 2 == 0);
        }

        for (int i = 0; i < 8; i++)
        {
            Assert.Equal(i % 2 == 0, prefix[i]);
        }
    }

    [Fact]
    public void RoundTrip_SliceAndAppend_PreservesBits()
    {
        var original = BitPrefix.Empty
            .Append(true).Append(false).Append(true)
            .Append(true).Append(false).Append(false);

        var firstHalf = original.Slice(0, 3);
        var secondHalf = original.Slice(3, 3);
        var reconstructed = firstHalf.Append(secondHalf);

        Assert.Equal(original.Length, reconstructed.Length);
        for (int i = 0; i < original.Length; i++)
        {
            Assert.Equal(original[i], reconstructed[i]);
        }
    }

    [Fact]
    public void FromBitArray_AllOnes_PreservesBits()
    {
        var bits = new BitArray(BitPrefix.MaxLength, true);
        var prefix = BitPrefix.FromBitArray(bits, 0, BitPrefix.MaxLength);

        Assert.Equal(BitPrefix.MaxLength, prefix.Length);
        for (int i = 0; i < BitPrefix.MaxLength; i++)
        {
            Assert.True(prefix[i], $"Bit {i} should be true");
        }
    }

    [Fact]
    public void FromBitArray_AllZeros_PreservesBits()
    {
        var bits = new BitArray(BitPrefix.MaxLength, false);
        var prefix = BitPrefix.FromBitArray(bits, 0, BitPrefix.MaxLength);

        Assert.Equal(BitPrefix.MaxLength, prefix.Length);
        for (int i = 0; i < BitPrefix.MaxLength; i++)
        {
            Assert.False(prefix[i], $"Bit {i} should be false");
        }
    }
}
