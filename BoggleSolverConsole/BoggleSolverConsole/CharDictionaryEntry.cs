namespace BoggleSolverConsole
{
    /// <summary>
    /// Tree dictionary lookup entry for a single char in a string
    /// </summary>
    public class CharDictionaryEntry
    {
        public bool IsWord { get; private set; }
        public string Word
        {
            get
            {
                if (!IsWord) throw new InvalidOperationException();
                return new string(GetChars().ToArray());
            }
        }

        protected CharDictionaryEntry? Previous { get; }
        protected char Last { get; private set; }
        public IEnumerable<CharDictionaryEntry> NextEntries => nextEntries;

        public CharDictionaryEntry(CharDictionaryEntry? previous, char last, bool word)
        {
            Previous = previous;
            Last = last;
            IsWord = word;
            nextEntries = Array.Empty<CharDictionaryEntry>();
        }

        IList<char>? nextChars;

        IList<CharDictionaryEntry> nextEntries;

        private IEnumerable<char> GetChars()
        {
            if (Previous == null)
                yield break;
            foreach (var ch in Previous.GetChars())
                yield return ch;
            yield return Last;
        }

        public CharDictionaryEntry? this[char next]
        {
            get
            {
                if (nextChars == null) return null;
                for (int i = nextEntries.Count - 1; i >= 0; i--)
                {
                    if (nextChars[i] == next)
                        return nextEntries[i];
                    if (nextChars[i] < next)
                        return null;
                }
                return null;
            }
            private set
            {
                if (nextChars == null)
                {
                    nextChars = new char[27];
                    nextEntries = new List<CharDictionaryEntry>(3);
                }
                else
                {
                    if (nextChars[nextEntries.Count - 1] > next)
                        throw new InvalidOperationException("unsorted input");
                }
                ArgumentNullException.ThrowIfNull(value);
                nextChars[nextEntries.Count] = next;

                nextEntries.Add(value);
            }
        }

        /// <summary>
        /// Creates entries for all characters in tail and adds them to this entry.
        /// </summary>
        /// <param name="tail"></param>
        public void AddWordTail(Span<char> tail)
        {
            var nextChar = this[tail[0]];
            var nextIsWord = tail.Length == 1;
            if (nextChar == null)
            {
                nextChar = new CharDictionaryEntry(this, tail[0], nextIsWord);
                this[tail[0]] = nextChar;
            }
            if (!nextIsWord) //more chars left
            {
                nextChar.AddWordTail(tail[1..]); // consume 1 char and recurse
            }
        }

        internal void WriteTo(BinaryWriter stream, int[] sizes)
        {
            // nextentries are at most 27, so size fits in a 5 bit field.
            byte size = (byte)nextEntries.Count;
            sizes[size]++;
            // So there is room to pack IsWord in the high bit.
            size |= (byte)(IsWord ? 0x80 : 0x00);
            // chars will be in the lowercase a-z range (hence the 27), so we can store them as a single byte.
            // When needed, we have still bits left in size to indicate extended encoding.
            var bytes = System.Text.Encoding.UTF8.GetBytes([Last]);
            if (bytes.Length != 1)
                throw new NotSupportedException("Non-ascii character in dictionary");
            stream.Write(size);
            stream.Write(bytes[0]);
            for (int i = 0; i < nextEntries.Count; i++)
            {
                nextEntries[i].WriteTo(stream, sizes);
            }
        }

        public static CharDictionaryEntry ReadFrom(BinaryReader reader, CharDictionaryEntry? previous = null)
        {
            byte size = reader.ReadByte();
            bool isWord = (size & 0x80) != 0;
            int count = size & 0x1F; // 5 bits for count (0-27)
            char last = (char)reader.ReadByte();
            var entry = new CharDictionaryEntry(previous, last, isWord);
            if (count > 0)
            {
                entry.nextEntries = new CharDictionaryEntry[count];
                entry.nextChars = new char[count];
                for (int i = 0; i < count; i++)
                {
                    var child = ReadFrom(reader, entry);
                    entry.nextEntries[i] = child;
                    entry.nextChars[i] = child.Last;
                }
            }
            return entry;
        }
    }
}
