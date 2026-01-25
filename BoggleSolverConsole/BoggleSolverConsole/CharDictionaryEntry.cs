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

        // Single-child optimization
        private char? singleChildChar;
        private CharDictionaryEntry? singleChildEntry;
        // Multi-child mode
        public IEnumerable<CharDictionaryEntry> NextEntries
        {
            get
            {
                if (singleChildChar.HasValue)
                {
                    yield return singleChildEntry!;
                }
                else if (nextEntries != null)
                {
                    foreach (var e in nextEntries)
                        yield return e;
                }
            }
        }

        public CharDictionaryEntry(CharDictionaryEntry? previous, char last, bool word)
        {
            Previous = previous;
            Last = last;
            IsWord = word;
            nextEntries = null;
            nextChars = null;
            singleChildChar = null;
            singleChildEntry = null;
        }

        IList<char>? nextChars;

        IList<CharDictionaryEntry>? nextEntries;

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
                // Single-child fast path
                if (singleChildChar.HasValue)
                {
                    if (singleChildChar.Value == next)
                        return singleChildEntry;
                    return null;
                }
                // Multi-child mode
                if (nextChars == null || nextEntries == null) return null;
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
                ArgumentNullException.ThrowIfNull(value);
                // No children yet
                if (!singleChildChar.HasValue && nextChars == null)
                {
                    singleChildChar = next;
                    singleChildEntry = value;
                    return;
                }
                // Already in single-child mode, need to upgrade to multi-child
                if (singleChildChar.HasValue && nextChars == null)
                {
                    nextChars = new char[27];
                    nextEntries = new List<CharDictionaryEntry>(3);
                    nextChars[0] = singleChildChar.Value;
                    nextEntries.Add(singleChildEntry!);
                    singleChildChar = null;
                    singleChildEntry = null;
                }
                // Multi-child mode
                if (nextChars![nextEntries!.Count - 1] > next)
                    throw new InvalidOperationException("unsorted input");
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

        internal void WriteTo(BinaryWriter stream)
        {
            // Determine number of children
            int count = 0;
            if (singleChildChar.HasValue)
                count = 1;
            else if (nextChars != null && nextEntries != null)
                count = nextEntries.Count;
            // nextentries are at most 27, so size fits in a 5 bit field.
            byte size = (byte)count;
            // So there is room to pack IsWord in the high bit.
            size |= (byte)(IsWord ? 0x80 : 0x00);
            // chars will be in the lowercase a-z range (hence the 27), so we can store them as a single byte.
            // When needed, we have still bits left in size to indicate extended encoding.
            var bytes = System.Text.Encoding.UTF8.GetBytes([Last]);
            if (bytes.Length != 1)
                throw new NotSupportedException("Non-ascii character in dictionary");
            stream.Write(size);
            stream.Write(bytes[0]);
            if (singleChildChar.HasValue)
            {
                singleChildEntry!.WriteTo(stream);
            }
            else if (nextChars != null && nextEntries != null)
            {
                for (int i = 0; i < nextEntries.Count; i++)
                {
                    nextEntries[i].WriteTo(stream);
                }
            }
        }

        public static CharDictionaryEntry ReadFrom(BinaryReader reader, CharDictionaryEntry? previous = null)
        {
            byte size = reader.ReadByte();
            bool isWord = (size & 0x80) != 0;
            int count = size & 0x1F; // 5 bits for count (0-27)
            char last = (char)reader.ReadByte();
            var entry = new CharDictionaryEntry(previous, last, isWord);
            if (count == 1)
            {
                var child = ReadFrom(reader, entry);
                entry.singleChildChar = child.Last;
                entry.singleChildEntry = child;
            }
            else if (count > 1)
            {
                entry.nextChars = new char[count];
                entry.nextEntries = new CharDictionaryEntry[count];
                for (int i = 0; i < count; i++)
                {
                    var child = ReadFrom(reader, entry);
                    entry.nextChars[i] = child.Last;
                    entry.nextEntries[i] = child;
                }
            }
            return entry;
        }
    }
}
