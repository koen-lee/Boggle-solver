using BitUtilities;

namespace FlatTrie;

/// <summary>
/// Variable-length integer encoding using a 2-bit class selector.
///
/// Class encoding:
///   00: value = 0 (no bits follow)
///   01: 2 bits follow (values 0-3, but typically used for 1-3)
///   10: 8 bits follow (values 0-255)
///   11: 18 bits follow (values 0-262143)
///
/// Total bits per class:
///   Class 0: 2 bits
///   Class 1: 4 bits
///   Class 2: 10 bits
///   Class 3: 20 bits
/// </summary>
public static class VarInt
{
    /// <summary>
    /// Bit widths for each class (excluding the 2-bit class selector).
    /// </summary>
    public static readonly int[] BitWidths = [0, 2, 8, 18];

    /// <summary>
    /// Maximum values for each class.
    /// </summary>
    public static readonly int[] MaxValues = [0, 3, 255, 262143];

    /// <summary>
    /// Calculate the total bits needed to encode a value.
    /// </summary>
    public static int GetEncodedBitCount(int value)
    {
        int classCode = GetClass(value);
        return 2 + BitWidths[classCode];
    }

    /// <summary>
    /// Get the class code (0-3) for a value.
    /// </summary>
    public static int GetClass(int value)
    {
        if (value == 0) return 0;
        if (value <= 3) return 1;
        if (value <= 255) return 2;
        return 3;
    }

    /// <summary>
    /// Write a variable-length integer.
    /// </summary>
    public static void Write(ref BitArrayWriter writer, int value)
    {
        int classCode = GetClass(value);
        writer.WriteBits((uint)classCode, 2);

        if (classCode > 0)
        {
            writer.WriteBits((uint)value, BitWidths[classCode]);
        }
    }

    /// <summary>
    /// Read a variable-length integer.
    /// </summary>
    public static int Read(ref BitArrayReader reader)
    {
        int classCode = (int)reader.ReadBits(2);

        if (classCode == 0)
            return 0;

        return (int)reader.ReadBits(BitWidths[classCode]);
    }

    /// <summary>
    /// Peek at a variable-length integer without advancing the reader.
    /// Returns the value and how many bits it occupies.
    /// </summary>
    public static (int value, int bitCount) Peek(ref BitArrayReader reader)
    {
        int startPos = reader.BitPosition;
        int value = Read(ref reader);
        int bitCount = reader.BitPosition - startPos;
        reader.Seek(startPos);
        return (value, bitCount);
    }

    /// <summary>
    /// Write a stale marker (0) while preserving the encoding class.
    /// This keeps the same bit width so layout isn't affected.
    /// </summary>
    public static void WriteStale(ref BitArrayWriter writer, int currentEncodingClass)
    {
        writer.WriteBits((uint)currentEncodingClass, 2);
        if (currentEncodingClass > 0)
        {
            writer.WriteBits(0, BitWidths[currentEncodingClass]);
        }
    }

    /// <summary>
    /// Write a value using a specific encoding class (for in-place updates).
    /// </summary>
    public static void WriteWithClass(ref BitArrayWriter writer, int value, int encodingClass)
    {
        writer.WriteBits((uint)encodingClass, 2);
        if (encodingClass > 0)
        {
            writer.WriteBits((uint)value, BitWidths[encodingClass]);
        }
    }
}
