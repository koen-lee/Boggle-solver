namespace BoggleSolverConsole
{
    /// <summary>
    /// Tree dictionary lookup entry for a single char in a string
    /// </summary>
    public class CharDictionaryEntry
    {
        private Dictionary<char, CharDictionaryEntry> _next;
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

        /// <summary>
        /// lazy-loaded wrapper.
        /// </summary>
        private Dictionary<char, CharDictionaryEntry> Next
        {
            get { return _next ?? (_next = new Dictionary<char, CharDictionaryEntry>(2)); }
        }

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
                CharDictionaryEntry nextChar;
                if (_next == null || !Next.TryGetValue(next, out nextChar))
                    return null;
                return nextChar;
            }
            set
            {
                Next[next] = value;
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
