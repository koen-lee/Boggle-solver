using System.Collections;

namespace BoggleSolverConsole.Bits
{

    /// <summary>
    /// Binary prefix trie (Patricia trie) where each character is represented as bits.
    /// Nodes with single children are collapsed into prefix runs.
    /// </summary>
    public class BinaryTrieNode
    {
        public bool IsWord { get; set; }
        public BitArray Prefix { get; set; } = new BitArray(0);
        public BinaryTrieNode? Left { get; set; }  // 0 branch
        public BinaryTrieNode? Right { get; set; } // 1 branch

        public bool HasChildren => Left != null || Right != null;

        /// <summary>
        /// Build binary trie from a list of words using default 8-bit encoding
        /// </summary>
        public static BinaryTrieNode BuildFromWords(IEnumerable<string> words)
            => BuildFromWords(words, CharEncoding.Ascii8Bit);

        /// <summary>
        /// Build binary trie from a list of words using specified encoding.
        /// Nodes are created with prefixes during insertion, with a final collapse pass
        /// to merge any remaining single-child chains.
        /// </summary>
        public static BinaryTrieNode BuildFromWords(IEnumerable<string> words, CharEncoding encoding)
        {
            var root = new BinaryTrieNode();

            foreach (var word in words)
            {
                var bits = encoding.StringToBits(word);
                root.Insert(bits, 0);
            }

            // Final pass to collapse any remaining single-child chains
            root.Collapse();

            return root;
        }

        /// <summary>
        /// Insert a word into the trie using default 8-bit encoding
        /// </summary>
        public void Insert(string word)
            => Insert(word, CharEncoding.Ascii8Bit);

        /// <summary>
        /// Insert a word into the trie using specified encoding
        /// </summary>
        public void Insert(string word, CharEncoding encoding)
        {
            var bits = encoding.StringToBits(word);
            Insert(bits, 0);
        }

        private void Insert(BitArray bits, int index)
        {
            // Match prefix first
            int prefixIndex = 0;
            while (prefixIndex < Prefix.Length && index < bits.Length)
            {
                if (bits[index] != Prefix[prefixIndex])
                {
                    // Divergence in prefix - split the node
                    SplitAt(prefixIndex, bits, index);
                    return;
                }
                prefixIndex++;
                index++;
            }

            if (prefixIndex < Prefix.Length)
            {
                // Word ends in the middle of prefix - split the node
                SplitAt(prefixIndex, bits, index);
                return;
            }

            // Prefix fully matched
            if (index == bits.Length)
            {
                IsWord = true;
                return;
            }

            // Continue to child
            bool bit = bits[index];
            if (bit)
            {
                if (Right == null)
                {
                    // Create new leaf node with remaining bits as prefix (already collapsed)
                    Right = CreateLeafWithPrefix(bits, index + 1);
                }
                else
                {
                    Right.Insert(bits, index + 1);
                }
            }
            else
            {
                if (Left == null)
                {
                    // Create new leaf node with remaining bits as prefix (already collapsed)
                    Left = CreateLeafWithPrefix(bits, index + 1);
                }
                else
                {
                    Left.Insert(bits, index + 1);
                }
            }
        }

        /// <summary>
        /// Create a new leaf node with the remaining bits as its prefix.
        /// This avoids creating a chain of single-child nodes that would need collapsing.
        /// </summary>
        private static BinaryTrieNode CreateLeafWithPrefix(BitArray bits, int startIndex)
        {
            return new BinaryTrieNode
            {
                IsWord = true,
                Prefix = SliceBitArray(bits, startIndex, bits.Length - startIndex)
            };
        }

        /// <summary>
        /// Create a new BitArray containing a slice of the source array.
        /// </summary>
        private static BitArray SliceBitArray(BitArray source, int start, int length)
        {
            if (length == 0)
                return new BitArray(0);
            var result = new BitArray(length);
            for (int i = 0; i < length; i++)
            {
                result[i] = source[start + i];
            }
            return result;
        }

        /// <summary>
        /// Split this node at the given prefix index, creating a child for the remaining prefix
        /// and inserting bits for a new word that diverges at this point.
        /// </summary>
        private void SplitAt(int splitIndex, BitArray newBits, int newBitIndex)
        {
            // Create child node with remainder of original prefix
            var child = new BinaryTrieNode
            {
                IsWord = IsWord,
                Left = Left,
                Right = Right
            };

            // Set child's prefix to remaining bits after split
            int remainingLength = Prefix.Length - splitIndex - 1;
            if (remainingLength > 0)
            {
                var remainingPrefix = new bool[remainingLength];
                for (int i = 0; i < remainingLength; i++)
                    remainingPrefix[i] = Prefix[splitIndex + 1 + i];
                child.Prefix = new BitArray(remainingPrefix);
            }

            // Determine which branch the original prefix continues on
            bool originalBit = Prefix[splitIndex];

            // Truncate this node's prefix
            if (splitIndex > 0)
            {
                var truncatedPrefix = new bool[splitIndex];
                for (int i = 0; i < splitIndex; i++)
                    truncatedPrefix[i] = Prefix[i];
                Prefix = new BitArray(truncatedPrefix);
            }
            else
            {
                Prefix = new BitArray(0);
            }

            // Reset this node - it becomes a branch point
            IsWord = false;
            Left = null;
            Right = null;

            // Place original content on appropriate branch
            if (originalBit)
                Right = child;
            else
                Left = child;

            // Now insert the new word from this point
            if (newBitIndex == newBits.Length)
            {
                // New word ends exactly at split point
                IsWord = true;
            }
            else
            {
                // New word continues - create/follow branch
                bool newBit = newBits[newBitIndex];
                if (newBit)
                {
                    Right ??= new BinaryTrieNode();
                    Right.Insert(newBits, newBitIndex + 1);
                }
                else
                {
                    Left ??= new BinaryTrieNode();
                    Left.Insert(newBits, newBitIndex + 1);
                }
            }
        }

        /// <summary>
        /// Collapse single-child chains into prefix runs
        /// </summary>
        private void Collapse()
        {
            // First, recursively collapse children
            Left?.Collapse();
            Right?.Collapse();

            // Now collapse this node's single-child chains
            // Collect prefix bits while we have exactly one child and are not a word
            var prefixBits = new List<bool>();

            while (!IsWord && (Left == null) != (Right == null))
            {
                // Exactly one child
                if (Left != null)
                {
                    prefixBits.Add(false); // 0
                    var child = Left;
                    // Absorb child's prefix
                    for (int i = 0; i < child.Prefix.Length; i++)
                        prefixBits.Add(child.Prefix[i]);
                    // Move child's data up
                    IsWord = child.IsWord;
                    Left = child.Left;
                    Right = child.Right;
                }
                else // Right != null
                {
                    prefixBits.Add(true); // 1
                    var child = Right!;
                    // Absorb child's prefix
                    for (int i = 0; i < child.Prefix.Length; i++)
                        prefixBits.Add(child.Prefix[i]);
                    // Move child's data up
                    IsWord = child.IsWord;
                    Left = child.Left;
                    Right = child.Right;
                }
            }

            if (prefixBits.Count > 0)
            {
                Prefix = new BitArray(prefixBits.ToArray());
            }
        }

        /// <summary>
        /// Chunk sizes indexed by 3-bit code. Must be in ascending order.
        /// Code 0 = no prefix, just the implicit branch bit, codes 1-7 = prefix chunk sizes.
        /// </summary>
        public static readonly int[] ChunkSizes = [0, 1, 2, 3, 4, 9, 14, 24];

        /// <summary>
        /// Serialize to bit stream using compact encoding:
        /// - 2 bits: IsWord + HasChildren
        /// - If dead end (IsWord=0, HasChildren=0): just 2 bits total
        /// - Otherwise: 3-bit chunk code + chunk bits + children
        /// </summary>
        public void WriteTo(BitWriter writer)
        {
            WriteTo(writer, Prefix, 0);
        }

        private static void WriteDeadEnd(BitWriter writer)
        {
            // IsWord = false, HasChildren = false
            writer.WriteBits(00, 2);
        }

        private void WriteTo(BitWriter writer, BitArray prefix, int prefixOffset)
        {
            int remaining = prefix.Length - prefixOffset;

            // Find largest chunk size that fits
            int chunkCode = 0;
            for (int i = ChunkSizes.Length - 1; i >= 1; i--)
            {
                if (remaining >= ChunkSizes[i])
                {
                    chunkCode = i;
                    break;
                }
            }
            int chunkSize = ChunkSizes[chunkCode];
            int afterChunk = remaining - chunkSize;

            // If we still have bits after this chunk, we need intermediate nodes
            if (afterChunk > 0)
            {
                // Intermediate node: not a word, has children
                writer.WriteBit(false); // IsWord = false
                writer.WriteBit(true);  // HasChildren = true
                writer.WriteBits((uint)chunkCode, 3);

                // Write chunk bits
                for (int i = 0; i < chunkSize; i++)
                {
                    writer.WriteBit(prefix[prefixOffset + i]);
                }

                // Next bit determines left (0) or right (1)
                // Always write both children - one is dead end, one continues
                bool nextBit = prefix[prefixOffset + chunkSize];
                if (nextBit)
                {
                    WriteDeadEnd(writer); // Left is dead end
                    WriteTo(writer, prefix, prefixOffset + chunkSize + 1); // Right continues
                }
                else
                {
                    WriteTo(writer, prefix, prefixOffset + chunkSize + 1); // Left continues
                    WriteDeadEnd(writer); // Right is dead end
                }
            }
            else
            {
                // Final node: write actual IsWord and children
                writer.WriteBit(IsWord);
                writer.WriteBit(HasChildren);
                writer.WriteBits((uint)chunkCode, 3);

                // Write chunk bits
                for (int i = 0; i < chunkSize; i++)
                {
                    writer.WriteBit(prefix[prefixOffset + i]);
                }

                // Always write both children (dead end if null)
                if (HasChildren)
                {
                    if (Left != null)
                        Left.WriteTo(writer);
                    else
                        WriteDeadEnd(writer);

                    if (Right != null)
                        Right.WriteTo(writer);
                    else
                        WriteDeadEnd(writer);
                }
            }
        }

        public static BinaryTrieNode ReadFrom(BitReader reader)
        {
            var node = new BinaryTrieNode();
            var prefixBits = new List<bool>();

            ReadInto(reader, node, prefixBits);

            node.Prefix = new BitArray(prefixBits.ToArray());
            return node;
        }

        /// <summary>
        /// Try to read a child node. Returns null if it's a dead end.
        /// </summary>
        private static BinaryTrieNode? TryReadChild(BitReader reader)
        {
            var node = new BinaryTrieNode();
            var prefixBits = new List<bool>();

            if (ReadInto(reader, node, prefixBits))
            {
                node.Prefix = new BitArray(prefixBits.ToArray());
                return node;
            }
            return null; // Dead end
        }

        /// <summary>
        /// Read node data into target. Returns false if this is a dead end.
        /// </summary>
        private static bool ReadInto(BitReader reader, BinaryTrieNode target, List<bool> prefixBits)
        {
            bool isWord = reader.ReadBit();
            bool hasChildren = reader.ReadBit();

            // Dead end: IsWord=0, HasChildren=0, no prefix code
            if (!isWord && !hasChildren)
            {
                return false;
            }

            // Read prefix chunk
            uint lengthCode = reader.ReadBits(3);
            int chunkSize = ChunkSizes[lengthCode];

            for (int i = 0; i < chunkSize; i++)
            {
                prefixBits.Add(reader.ReadBit());
            }

            if (hasChildren)
            {
                // Always read both children
                var left = TryReadChild(reader);
                var right = TryReadChild(reader);

                // Check if this is an intermediate node (one dead end = prefix bit)
                if (!isWord && (left == null) != (right == null))
                {
                    // Add implicit branch bit to prefix
                    prefixBits.Add(right != null); // left=0, right=1

                    // Absorb the non-null child's prefix and data
                    var child = left ?? right!;
                    for (int i = 0; i < child.Prefix.Length; i++)
                        prefixBits.Add(child.Prefix[i]);

                    target.IsWord = child.IsWord;
                    target.Left = child.Left;
                    target.Right = child.Right;
                }
                else
                {
                    // Real branch point or word with one child
                    target.IsWord = isWord;
                    target.Left = left;
                    target.Right = right;
                }
            }
            else
            {
                // Leaf node (must be a word since we passed dead end check)
                target.IsWord = isWord;
            }

            return true;
        }

        /// <summary>
        /// Check if a word exists in the trie (using default 8-bit encoding)
        /// </summary>
        public bool Contains(string word)
            => Contains(word, CharEncoding.Ascii8Bit);

        /// <summary>
        /// Check if a word exists in the trie using specified encoding
        /// </summary>
        public bool Contains(string word, CharEncoding encoding)
        {
            var bits = encoding.StringToBits(word);
            return Contains(bits, 0);
        }

        private bool Contains(BitArray bits, int bitIndex)
        {
            // Match prefix
            for (int i = 0; i < Prefix.Length; i++)
            {
                if (bitIndex >= bits.Length)
                    return false;
                if (bits[bitIndex] != Prefix[i])
                    return false;
                bitIndex++;
            }

            // End of word bits?
            if (bitIndex == bits.Length)
                return IsWord;

            // Follow child
            bool bit = bits[bitIndex];
            var child = bit ? Right : Left;
            if (child == null)
                return false;

            return child.Contains(bits, bitIndex + 1);
        }

        /// <summary>
        /// Enumerate all words stored in the trie (using default 8-bit encoding)
        /// </summary>
        public IEnumerable<string> EnumerateWords()
            => EnumerateWords(CharEncoding.Ascii8Bit);

        /// <summary>
        /// Enumerate all words stored in the trie using specified encoding
        /// </summary>
        public IEnumerable<string> EnumerateWords(CharEncoding encoding)
        {
            var bits = new List<bool>();
            return EnumerateWords(bits, encoding);
        }

        private IEnumerable<string> EnumerateWords(List<bool> bits, CharEncoding encoding)
        {
            // Add this node's prefix to the path
            for (int i = 0; i < Prefix.Length; i++)
                bits.Add(Prefix[i]);

            // If this is a word, convert bits to string and yield
            if (IsWord)
            {
                yield return encoding.BitsToString(bits);
            }

            // Recurse to children
            if (Left != null)
            {
                bits.Add(false); // 0 for left
                foreach (var word in Left.EnumerateWords(bits, encoding))
                    yield return word;
                bits.RemoveAt(bits.Count - 1);
            }
            if (Right != null)
            {
                bits.Add(true); // 1 for right
                foreach (var word in Right.EnumerateWords(bits, encoding))
                    yield return word;
                bits.RemoveAt(bits.Count - 1);
            }

            // Remove this node's prefix from the path
            bits.RemoveRange(bits.Count - Prefix.Length, Prefix.Length);
        }

        /// <summary>
        /// Get statistics about the trie
        /// </summary>
        public (int nodeCount, int totalPrefixBits, int wordCount) GetStats()
        {
            var (nodes, prefixBits, words, _) = GetStatsWithHistogram();
            return (nodes, prefixBits, words);
        }

        /// <summary>
        /// Get statistics about the trie including prefix size histogram
        /// </summary>
        public (int nodeCount, int totalPrefixBits, int wordCount, Dictionary<int, int> prefixHistogram) GetStatsWithHistogram()
        {
            var histogram = new Dictionary<int, int>();
            GetStatsRecursive(histogram, out int nodes, out int prefixBits, out int words);
            return (nodes, prefixBits, words, histogram);
        }

        private void GetStatsRecursive(Dictionary<int, int> histogram, out int nodes, out int prefixBits, out int words)
        {
            nodes = 1;
            prefixBits = Prefix.Length;
            words = IsWord ? 1 : 0;

            // Track prefix length in histogram
            int len = Prefix.Length;
            histogram.TryGetValue(len, out int count);
            histogram[len] = count + 1;

            if (Left != null)
            {
                Left.GetStatsRecursive(histogram, out int n, out int p, out int w);
                nodes += n;
                prefixBits += p;
                words += w;
            }
            if (Right != null)
            {
                Right.GetStatsRecursive(histogram, out int n, out int p, out int w);
                nodes += n;
                prefixBits += p;
                words += w;
            }
        }
    }
}
