using System.Collections;

namespace BoggleSolverConsole.Bits
{
    /// <summary>
    /// Character encoding strategy for binary trie
    /// </summary>
    public class CharEncoding
    {
        public Func<string, BitArray> StringToBits { get; }
        public Func<BitArray, string> BitsToString { get; }
        public int BitsPerChar { get; }

        private CharEncoding(int bitsPerChar, Func<string, BitArray> stringToBits, Func<BitArray, string> bitsToString)
        {
            BitsPerChar = bitsPerChar;
            StringToBits = stringToBits;
            BitsToString = bitsToString;
        }

        /// <summary>
        /// 8-bit encoding: each character is stored as its ASCII byte value
        /// </summary>
        public static CharEncoding Ascii8Bit { get; } = new CharEncoding(
            8,
            s =>
            {
                // Convert string to byte array, then construct BitArray directly
                var bytes = new byte[s.Length];
                for (int i = 0; i < s.Length; i++)
                    bytes[i] = (byte)s[i];
                return new BitArray(bytes);
            },
            bits =>
            {
                if (bits.Count % 8 != 0)
                    throw new InvalidOperationException("Bit count must be multiple of 8");

                // Extract bytes directly using CopyTo
                var bytes = new byte[bits.Count / 8];
                bits.CopyTo(bytes, 0);

                // Convert bytes to string
                var chars = new char[bytes.Length];
                for (int i = 0; i < bytes.Length; i++)
                    chars[i] = (char)bytes[i];
                return new string(chars);
            }
        );

        /// <summary>
        /// 5-bit encoding: a-z=0-25, space=26, '\0'=27 (matches CharDictionaryEntry)
        /// </summary>
        public static CharEncoding Compact5Bit { get; } = new CharEncoding(
            5,
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

                return new BitArray(ints) { Length = totalBits };
            },
            bits =>
            {
                if (bits.Count % 5 != 0)
                    throw new InvalidOperationException("Bit count must be multiple of 5");

                var ints = new int[(bits.Count + 31) / 32];
                bits.CopyTo(ints, 0);

                var chars = new char[bits.Count / 5];
                for (int i = 0; i < chars.Length; i++)
                {
                    int bitPos = i * 5;
                    int intIndex = bitPos / 32;
                    int bitOffset = bitPos % 32;

                    // Use uint to avoid sign-extension on right shift
                    int value = (int)(((uint)ints[intIndex] >> bitOffset) & 0x1F);
                    if (bitOffset > 27) // Spans two ints
                        value |= (int)(((uint)ints[intIndex + 1] << (32 - bitOffset)) & 0x1F);

                    chars[i] = Char5BitToChar(value);
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
}
