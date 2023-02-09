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

        protected CharDictionaryEntry Previous { get; }
        protected char Last { get; private set; }

        public CharDictionaryEntry(CharDictionaryEntry previous, char last, bool word)
        {
            Previous = previous;
            Last = last;
            IsWord = word;
        }

        IList<char> nextChars;
        IList<CharDictionaryEntry> nextEntries;

        private IEnumerable<char> GetChars()
        {
            if (Last == char.MinValue)
                yield break;
            foreach (var ch in Previous.GetChars())
                yield return ch;
            yield return Last;
        }

        public CharDictionaryEntry this[char next]
        {
            get
            {
                if (nextChars == null) return null;
                for (int i = nextEntries.Count - 1; i >= 0; i--)
                {
                    if (nextChars[i] == next)
                        return nextEntries[i]; 
                    if( nextChars[i] < next)
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
                } else
                {
                    if (nextChars[nextEntries.Count - 1] > next)
                        throw new InvalidOperationException("unsorted input");
                }
                
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
            CharDictionaryEntry nextChar = this[tail[0]];
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
    }
}
