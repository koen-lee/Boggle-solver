using System.Collections;

namespace BoggleSolverConsole.Bits
{
    /// <summary>
    /// Character encoding strategy for binary trie
    /// </summary>
    public class CharEncoding
    {
        public Func<string, BitArray> StringToBits { get; }
        public Func<List<bool>, string> BitsToString { get; }
        public int BitsPerChar { get; }

        private CharEncoding(int bitsPerChar, Func<string, BitArray> stringToBits, Func<List<bool>, string> bitsToString)
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
                var bits = new BitArray(s.Length * 8);
                for (int i = 0; i < s.Length; i++)
                {
                    byte c = (byte)s[i];
                    for (int b = 0; b < 8; b++)
                    {
                        bits[i * 8 + b] = (c & (1 << b)) != 0;
                    }
                }
                return bits;
            },
            bits =>
            {
                if (bits.Count % 8 != 0)
                    throw new InvalidOperationException("Bit count must be multiple of 8");

                var chars = new char[bits.Count / 8];
                for (int i = 0; i < chars.Length; i++)
                {
                    byte c = 0;
                    for (int b = 0; b < 8; b++)
                    {
                        if (bits[i * 8 + b])
                            c |= (byte)(1 << b);
                    }
                    chars[i] = (char)c;
                }
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
                var bits = new BitArray(s.Length * 5);
                for (int i = 0; i < s.Length; i++)
                {
                    int value = CharTo5Bit(s[i]);
                    for (int b = 0; b < 5; b++)
                    {
                        bits[i * 5 + b] = (value & (1 << b)) != 0;
                    }
                }
                return bits;
            },
            bits =>
            {
                if (bits.Count % 5 != 0)
                    throw new InvalidOperationException("Bit count must be multiple of 5");

                var chars = new char[bits.Count / 5];
                for (int i = 0; i < chars.Length; i++)
                {
                    int value = 0;
                    for (int b = 0; b < 5; b++)
                    {
                        if (bits[i * 5 + b])
                            value |= 1 << b;
                    }
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
