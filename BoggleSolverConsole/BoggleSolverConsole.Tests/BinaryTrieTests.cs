using System.Diagnostics;
using BoggleSolverConsole.Bits;
using Xunit;
using Xunit.Abstractions;

namespace BoggleSolverConsole.Tests;

public class BinaryTrieTests
{
    private readonly ITestOutputHelper _output;

    public BinaryTrieTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void BuildFromWords_SingleWord_ContainsIt()
    {
        var trie = BinaryTrieNode.BuildFromWords(["cat"]);

        Assert.True(trie.Contains("cat"));
        Assert.False(trie.Contains("car"));
        Assert.False(trie.Contains("cats"));
        Assert.False(trie.Contains("ca"));
    }

    [Fact]
    public void BuildFromWords_MultipleWords_ContainsAll()
    {
        var words = new[] { "cat", "car", "card", "care", "careful" };
        var trie = BinaryTrieNode.BuildFromWords(words);

        foreach (var word in words)
        {
            Assert.True(trie.Contains(word), $"Should contain '{word}'");
        }

        Assert.False(trie.Contains("ca"));
        Assert.False(trie.Contains("c"));
        Assert.False(trie.Contains("cards"));
        Assert.False(trie.Contains("dog"));
    }

    [Fact]
    public void BuildFromWords_PrefixWords_AllPresent()
    {
        var words = new[] { "a", "an", "ant", "anti", "anticipate" };
        var trie = BinaryTrieNode.BuildFromWords(words);

        foreach (var word in words)
        {
            Assert.True(trie.Contains(word), $"Should contain '{word}'");
        }
    }

    [Fact]
    public void GetStats_ReturnsCorrectWordCount()
    {
        var words = new[] { "cat", "car", "card", "care", "careful" };
        var trie = BinaryTrieNode.BuildFromWords(words);

        var (_, _, wordCount) = trie.GetStats();

        Assert.Equal(5, wordCount);
    }

    [Fact]
    public void Serialization_SingleWord_Roundtrip()
    {
        var original = BinaryTrieNode.BuildFromWords(["hello"]);

        using var ms = new MemoryStream();
        var writer = new BitWriter(ms);
        original.WriteTo(writer);
        writer.Flush();

        ms.Position = 0;
        var reader = new BitReader(ms);
        var restored = BinaryTrieNode.ReadFrom(reader);

        Assert.True(restored.Contains("hello"));
        Assert.False(restored.Contains("hell"));
        Assert.False(restored.Contains("helloo"));
    }

    [Fact]
    public void Serialization_MultipleWords_Roundtrip()
    {
        var words = new[] { "cat", "car", "card", "care", "careful", "dog", "door" };
        var original = BinaryTrieNode.BuildFromWords(words);

        using var ms = new MemoryStream();
        var writer = new BitWriter(ms);
        original.WriteTo(writer);
        writer.Flush();

        ms.Position = 0;
        var reader = new BitReader(ms);
        var restored = BinaryTrieNode.ReadFrom(reader);

        foreach (var word in words)
        {
            Assert.True(restored.Contains(word), $"Restored trie should contain '{word}'");
        }

        Assert.False(restored.Contains("ca"));
        Assert.False(restored.Contains("cards"));
    }

    [Fact]
    public void Serialization_PrefixWords_Roundtrip()
    {
        var words = new[] { "a", "an", "ant", "anti", "anticipate" };
        var original = BinaryTrieNode.BuildFromWords(words);

        using var ms = new MemoryStream();
        var writer = new BitWriter(ms);
        original.WriteTo(writer);
        writer.Flush();

        ms.Position = 0;
        var reader = new BitReader(ms);
        var restored = BinaryTrieNode.ReadFrom(reader);

        foreach (var word in words)
        {
            Assert.True(restored.Contains(word), $"Restored trie should contain '{word}'");
        }
    }

    [Fact]
    public void Serialization_LongWord_Roundtrip()
    {
        // Test word longer than 32 bits (4 chars = 32 bits, so 5+ chars tests long prefixes)
        var words = new[] { "abcdefghijklmnopqrstuvwxyz" };
        var original = BinaryTrieNode.BuildFromWords(words);

        using var ms = new MemoryStream();
        var writer = new BitWriter(ms);
        original.WriteTo(writer);
        writer.Flush();

        ms.Position = 0;
        var reader = new BitReader(ms);
        var restored = BinaryTrieNode.ReadFrom(reader);

        Assert.True(restored.Contains("abcdefghijklmnopqrstuvwxyz"));
    }

    [Fact]
    public void Serialization_ManyWords_Roundtrip()
    {
        var words = new[]
        {
            "the", "be", "to", "of", "and", "a", "in", "that", "have", "i",
            "it", "for", "not", "on", "with", "he", "as", "you", "do", "at",
            "this", "but", "his", "by", "from", "they", "we", "say", "her", "she"
        };
        var original = BinaryTrieNode.BuildFromWords(words);

        using var ms = new MemoryStream();
        var writer = new BitWriter(ms);
        original.WriteTo(writer);
        writer.Flush();

        ms.Position = 0;
        var reader = new BitReader(ms);
        var restored = BinaryTrieNode.ReadFrom(reader);

        foreach (var word in words)
        {
            Assert.True(restored.Contains(word), $"Restored trie should contain '{word}'");
        }

        // Verify stats match
        var (origNodes, origPrefix, origWords) = original.GetStats();
        var (restNodes, restPrefix, restWords) = restored.GetStats();

        Assert.Equal(origWords, restWords);
    }

    [Fact]
    public void Serialization_EmptyTrie_Roundtrip()
    {
        var original = BinaryTrieNode.BuildFromWords([]);

        using var ms = new MemoryStream();
        var writer = new BitWriter(ms);
        original.WriteTo(writer);
        writer.Flush();

        ms.Position = 0;
        var reader = new BitReader(ms);
        var restored = BinaryTrieNode.ReadFrom(reader);

        Assert.False(restored.Contains("anything"));
        var (_, _, wordCount) = restored.GetStats();
        Assert.Equal(0, wordCount);
    }

    [Fact]
    public void Serialization_SingleCharWords_Roundtrip()
    {
        var words = new[] { "a", "b", "c", "x", "y", "z" };
        var original = BinaryTrieNode.BuildFromWords(words);

        using var ms = new MemoryStream();
        var writer = new BitWriter(ms);
        original.WriteTo(writer);
        writer.Flush();

        ms.Position = 0;
        var reader = new BitReader(ms);
        var restored = BinaryTrieNode.ReadFrom(reader);

        foreach (var word in words)
        {
            Assert.True(restored.Contains(word), $"Restored trie should contain '{word}'");
        }
    }

    [Fact]
    public void Contains_PartialMatch_ReturnsFalse()
    {
        var trie = BinaryTrieNode.BuildFromWords(["testing"]);

        Assert.False(trie.Contains("test"));
        Assert.False(trie.Contains("testi"));
        Assert.False(trie.Contains("testin"));
        Assert.True(trie.Contains("testing"));
    }

    [Fact]
    public void Contains_LongerThanWord_ReturnsFalse()
    {
        var trie = BinaryTrieNode.BuildFromWords(["test"]);

        Assert.True(trie.Contains("test"));
        Assert.False(trie.Contains("testing"));
        Assert.False(trie.Contains("tests"));
    }

    [Fact]
    public void Collapse_ReducesNodeCount()
    {
        // Without collapse, "cat" would need 3*8 = 24 nodes (one per bit)
        // With collapse, the single-child chain should be compressed
        var trie = BinaryTrieNode.BuildFromWords(["cat"]);

        var (nodeCount, _, _) = trie.GetStats();

        // After collapse, we should have far fewer than 24 nodes
        Assert.True(nodeCount < 3, $"Expected collapsed node count < 3, got {nodeCount}");
    }

    [Fact]
    public void EnumerateWords_ReturnsAllWords()
    {
        var words = new[] { "cat", "car", "card", "care", "dog" };
        var trie = BinaryTrieNode.BuildFromWords(words);

        var enumerated = trie.EnumerateWords().ToHashSet();

        Assert.Equal(words.Length, enumerated.Count);
        foreach (var word in words)
        {
            Assert.Contains(word, enumerated);
        }
    }

    [Fact]
    public void EnumerateWords_AfterSerialization_ReturnsAllWords()
    {
        var words = new[] { "apple", "application", "apply", "banana", "band" };
        var original = BinaryTrieNode.BuildFromWords(words);

        using var ms = new MemoryStream();
        var writer = new BitWriter(ms);
        original.WriteTo(writer);
        writer.Flush();

        ms.Position = 0;
        var reader = new BitReader(ms);
        var restored = BinaryTrieNode.ReadFrom(reader);

        var enumerated = restored.EnumerateWords().ToHashSet();

        Assert.Equal(words.Length, enumerated.Count);
        foreach (var word in words)
        {
            Assert.Contains(word, enumerated);
        }
    }

    [Fact]
    public void EnumerateWords_PrefixWords_ReturnsAll()
    {
        var words = new[] { "a", "an", "ant", "anti" };
        var trie = BinaryTrieNode.BuildFromWords(words);

        var enumerated = trie.EnumerateWords().ToHashSet();

        Assert.Equal(words.Length, enumerated.Count);
        foreach (var word in words)
        {
            Assert.Contains(word, enumerated);
        }
    }

    [Fact]
    public void EnumerateWords_EmptyTrie_ReturnsNothing()
    {
        var trie = BinaryTrieNode.BuildFromWords([]);

        var enumerated = trie.EnumerateWords().ToList();

        Assert.Empty(enumerated);
    }

    [Fact]
    public void Compact5Bit_BuildAndContains()
    {
        var words = new[] { "cat", "car", "dog", "hello world" };
        var trie = BinaryTrieNode.BuildFromWords(words, CharEncoding.Compact5Bit);

        foreach (var word in words)
        {
            Assert.True(trie.Contains(word, CharEncoding.Compact5Bit), $"Should contain '{word}'");
        }

        Assert.False(trie.Contains("cats", CharEncoding.Compact5Bit));
    }

    [Fact]
    public void Compact5Bit_EnumerateWords()
    {
        var words = new[] { "apple", "ant", "banana" };
        var trie = BinaryTrieNode.BuildFromWords(words, CharEncoding.Compact5Bit);

        var enumerated = trie.EnumerateWords(CharEncoding.Compact5Bit).ToHashSet();

        Assert.Equal(words.Length, enumerated.Count);
        foreach (var word in words)
        {
            Assert.Contains(word, enumerated);
        }
    }

    [Fact]
    public void Compact5Bit_Serialization_Roundtrip()
    {
        var words = new[] { "test", "testing", "tested", "the quick brown fox" };
        var original = BinaryTrieNode.BuildFromWords(words, CharEncoding.Compact5Bit);

        using var ms = new MemoryStream();
        var writer = new BitWriter(ms);
        original.WriteTo(writer);
        writer.Flush();

        ms.Position = 0;
        var reader = new BitReader(ms);
        var restored = BinaryTrieNode.ReadFrom(reader);

        var enumerated = restored.EnumerateWords(CharEncoding.Compact5Bit).ToHashSet();

        Assert.Equal(words.Length, enumerated.Count);
        foreach (var word in words)
        {
            Assert.Contains(word, enumerated);
        }
    }

    [Fact]
    public void Compact5Bit_SmallerThan8Bit()
    {
        var words = new[] { "cat", "car", "card", "care", "careful", "dog", "door" };

        var trie8Bit = BinaryTrieNode.BuildFromWords(words, CharEncoding.Ascii8Bit);
        var trie5Bit = BinaryTrieNode.BuildFromWords(words, CharEncoding.Compact5Bit);

        using var ms8 = new MemoryStream();
        var writer8 = new BitWriter(ms8);
        trie8Bit.WriteTo(writer8);
        writer8.Flush();

        using var ms5 = new MemoryStream();
        var writer5 = new BitWriter(ms5);
        trie5Bit.WriteTo(writer5);
        writer5.Flush();

        // 5-bit encoding should produce smaller output
        Assert.True(ms5.Length < ms8.Length,
            $"5-bit ({ms5.Length} bytes) should be smaller than 8-bit ({ms8.Length} bytes)");
    }

    [Fact]
    public void Woorden_FullDictionary_RoundtripWithStats()
    {
        // Load woorden.txt (Dutch word list)
        var words = File.ReadAllLines("woorden.txt")
            .Select(w => w.ToLowerInvariant())
            .ToArray();

        _output.WriteLine($"Loaded {words.Length} words");

        // Build trie with timing
        var sw = Stopwatch.StartNew();
        var trie = BinaryTrieNode.BuildFromWords(words, CharEncoding.Compact5Bit);
        var buildTime = sw.Elapsed;

        // Get stats with histogram
        var (nodeCount, totalPrefixBits, wordCount, histogram) = trie.GetStatsWithHistogram();
        _output.WriteLine($"Build time: {buildTime.TotalMilliseconds:F1}ms");
        _output.WriteLine($"Nodes: {nodeCount}, Total prefix bits: {totalPrefixBits}, Words: {wordCount}");

        // Print prefix histogram
        _output.WriteLine("\nPrefix size histogram:");
        foreach (var kvp in histogram.OrderBy(k => k.Key))
        {
            _output.WriteLine($"  {kvp.Key,3} bits: {kvp.Value,6} nodes");
        }

        // Serialize with timing
        sw.Restart();
        using var ms = new MemoryStream();
        var writer = new BitWriter(ms);
        trie.WriteTo(writer);
        writer.Flush();
        var serializeTime = sw.Elapsed;

        _output.WriteLine($"\nSerialize time: {serializeTime.TotalMilliseconds:F1}ms");
        _output.WriteLine($"Serialized size: {ms.Length:N0} bytes ({ms.Length * 8:N0} bits)");

        // Deserialize with timing
        ms.Position = 0;
        sw.Restart();
        var reader = new BitReader(ms);
        var restored = BinaryTrieNode.ReadFrom(reader);
        var deserializeTime = sw.Elapsed;

        _output.WriteLine($"Deserialize time: {deserializeTime.TotalMilliseconds:F1}ms");

        // Verify roundtrip
        var (restoredNodes, _, restoredWords, _) = restored.GetStatsWithHistogram();
        Assert.Equal(wordCount, restoredWords);

        // Spot check some words
        Assert.True(restored.Contains("aachen", CharEncoding.Compact5Bit));
        Assert.True(restored.Contains("a capella", CharEncoding.Compact5Bit));
    }
}
