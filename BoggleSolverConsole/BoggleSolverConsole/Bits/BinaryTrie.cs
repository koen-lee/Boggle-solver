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
        /// Build binary trie from a list of words using specified encoding
        /// </summary>
        public static BinaryTrieNode BuildFromWords(IEnumerable<string> words, CharEncoding encoding)
        {
            var root = new BinaryTrieNode();

            foreach (var word in words)
            {
                var bits = encoding.StringToBits(word);
                Insert(root, bits, 0);
            }

            // Collapse single-child chains into prefixes
            Collapse(root);

            return root;
        }

        private static void Insert(BinaryTrieNode node, BitArray bits, int index)
        {
            if (index == bits.Length)
            {
                node.IsWord = true;
                return;
            }

            bool bit = bits[index];
            if (bit)
            {
                node.Right ??= new BinaryTrieNode();
                Insert(node.Right, bits, index + 1);
            }
            else
            {
                node.Left ??= new BinaryTrieNode();
                Insert(node.Left, bits, index + 1);
            }
        }

        /// <summary>
        /// Collapse single-child chains into prefix runs
        /// </summary>
        private static void Collapse(BinaryTrieNode node)
        {
            // First, recursively collapse children
            if (node.Left != null) Collapse(node.Left);
            if (node.Right != null) Collapse(node.Right);

            // Now collapse this node's single-child chains
            // Collect prefix bits while we have exactly one child and are not a word
            var prefixBits = new List<bool>();

            while (!node.IsWord && (node.Left == null) != (node.Right == null))
            {
                // Exactly one child
                if (node.Left != null)
                {
                    prefixBits.Add(false); // 0
                    var child = node.Left;
                    // Absorb child's prefix
                    for (int i = 0; i < child.Prefix.Length; i++)
                        prefixBits.Add(child.Prefix[i]);
                    // Move child's data up
                    node.IsWord = child.IsWord;
                    node.Left = child.Left;
                    node.Right = child.Right;
                }
                else // node.Right != null
                {
                    prefixBits.Add(true); // 1
                    var child = node.Right;
                    // Absorb child's prefix
                    for (int i = 0; i < child.Prefix.Length; i++)
                        prefixBits.Add(child.Prefix[i]);
                    // Move child's data up
                    node.IsWord = child.IsWord;
                    node.Left = child.Left;
                    node.Right = child.Right;
                }
            }

            if (prefixBits.Count > 0)
            {
                node.Prefix = new BitArray(prefixBits.ToArray());
            }
        }

        /// <summary>
        /// Serialize to bit stream using compact encoding:
        /// 1 bit: IsWord
        /// 1 bit: HasChildren
        /// If IsWord=0 and HasChildren=0: dead end (just 2 bits total)
        /// Otherwise:
        ///   2 bits: prefix length (00=0, 01=2 bits, 10=8 bits, 11=32 bits)
        ///   [prefix bits - fixed length based on code]
        ///   If HasChildren: [left subtree][right subtree]
        ///
        /// Non-existent branches are encoded as dead ends (2 bits).
        /// </summary>
        public void WriteTo(BitWriter writer)
        {
            WriteTo(writer, Prefix, 0);
        }

        private static void WriteDeadEnd(BitWriter writer)
        {
            writer.WriteBit(false); // IsWord = false
            writer.WriteBit(false); // HasChildren = false
            // No prefix code - dead end is just 2 bits
        }

        private void WriteTo(BitWriter writer, BitArray prefix, int prefixOffset)
        {
            int remaining = prefix.Length - prefixOffset;

            // Determine chunk size: 32, 8, 2, or 0
            int chunkSize;
            uint chunkCode;
            if (remaining >= 32)
            {
                chunkSize = 32;
                chunkCode = 0b11;
            }
            else if (remaining >= 8)
            {
                chunkSize = 8;
                chunkCode = 0b10;
            }
            else if (remaining >= 2)
            {
                chunkSize = 2;
                chunkCode = 0b01;
            }
            else
            {
                chunkSize = 0;
                chunkCode = 0b00;
            }

            int afterChunk = remaining - chunkSize;

            // If we still have bits after this chunk, we need intermediate nodes
            if (afterChunk > 0)
            {
                // Intermediate node: not a word, has children
                writer.WriteBit(false); // IsWord = false
                writer.WriteBit(true);  // HasChildren = true
                writer.WriteBits(chunkCode, 2);

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
                writer.WriteBits(chunkCode, 2);

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
            uint lengthCode = reader.ReadBits(2);
            int chunkSize = lengthCode switch
            {
                0b00 => 0,
                0b01 => 2,
                0b10 => 8,
                0b11 => 32,
                _ => throw new InvalidOperationException()
            };

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
            int nodes = 1;
            int prefixBits = Prefix.Length;
            int words = IsWord ? 1 : 0;

            if (Left != null)
            {
                var (n, p, w) = Left.GetStats();
                nodes += n;
                prefixBits += p;
                words += w;
            }
            if (Right != null)
            {
                var (n, p, w) = Right.GetStats();
                nodes += n;
                prefixBits += p;
                words += w;
            }

            return (nodes, prefixBits, words);
        }
    }
}
