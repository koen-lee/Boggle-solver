namespace BitUtilities;

/// <summary>
/// Character encoding strategy for binary trie
/// </summary>
public class CharEncoding
{
    public Func<string, BitString> StringToBits { get; }
    public Func<BitString, string> BitsToString { get; }

    private CharEncoding(Func<string, BitString> stringToBits, Func<BitString, string> bitsToString)
    {
        StringToBits = stringToBits;
        BitsToString = bitsToString;
    }

    /// <summary>
    /// UTF-8 encoding: each character is stored as 1-4 bytes using UTF-8 encoding
    /// </summary>
    public static CharEncoding Utf8 { get; } = new CharEncoding(
        s => BitString.FromBytes(System.Text.Encoding.UTF8.GetBytes(s)),
        bits =>
        {
            if (bits.Length % 8 != 0)
                throw new InvalidOperationException("Bit count must be multiple of 8");

            var bytes = new byte[bits.Length / 8];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte)bits.ToBitPrefix(i * 8, 8).Bits;
            }
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
    );

    /// <summary>
    /// 5-bit encoding: a-z=0-25, space=26, '\0'=27 (matches CharDictionaryEntry)
    /// </summary>
    public static CharEncoding Compact5Bit { get; } = new CharEncoding(
        s =>
        {
            int totalBits = s.Length * 5;
            var ints = new int[(totalBits + 31) / 32];

            for (int i = 0; i < s.Length; i++)
            {
                int value = CharTo5Bit(s[i]);
                int bitPos = i * 5;
                int intIndex = bitPos / 32;
                int bitOffset = bitPos % 32;

                ints[intIndex] |= value << bitOffset;
                if (bitOffset > 27) // Overflow into next int
                    ints[intIndex + 1] |= value >> (32 - bitOffset);
            }

            return BitString.FromInts(ints, totalBits);
        },
        bits =>
        {
            if (bits.Length % 5 != 0)
                throw new InvalidOperationException("Bit count must be multiple of 5");

            var chars = new char[bits.Length / 5];
            for (int i = 0; i < chars.Length; i++)
            {
                // Extract 5 bits efficiently using ToBitPrefix
                chars[i] = Char5BitToChar((int)bits.ToBitPrefix(i * 5, 5).Bits);
            }
            return new string(chars);
        }
    );

    private static int CharTo5Bit(char c)
    {
        if (c >= 'a' && c <= 'z')
            return c - 'a';
        if (c == ' ')
            return 26;
        if (c == char.MinValue)
            return 27;
        throw new NotSupportedException($"Character '{c}' not supported in 5-bit encoding");
    }

    private static char Char5BitToChar(int value)
    {
        if (value <= 25)
            return (char)('a' + value);
        if (value == 26)
            return ' ';
        if (value == 27)
            return char.MinValue;
        throw new NotSupportedException($"Invalid 5-bit value {value}");
    }
}
