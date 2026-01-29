using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace FlatTrie.Tests;

public class FlatTrieTests
{
    private readonly ITestOutputHelper _output;

    public FlatTrieTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void EmptyTrie_TryRead_ReturnsFalse()
    {
        var trie = new FlatTrie();

        bool found = trie.TryRead("hello", out long value);

        Assert.False(found);
        Assert.Equal(0, value);
    }

    [Fact]
    public void SingleKey_WriteAndRead()
    {
        var trie = new FlatTrie();

        bool written = trie.TryWrite("hello", 42);
        Assert.True(written);

        bool found = trie.TryRead("hello", out long value);
        Assert.True(found);
        Assert.Equal(42, value);
    }

    [Fact]
    public void SingleKey_ReadNonExistent_ReturnsFalse()
    {
        var trie = new FlatTrie();

        trie.TryWrite("hello", 42);

        Assert.False(trie.TryRead("world", out _));
        Assert.False(trie.TryRead("hell", out _));
        Assert.False(trie.TryRead("helloo", out _));
    }

    [Fact]
    public void MultipleKeys_NoSharedPrefix()
    {
        var trie = new FlatTrie();

        Assert.True(trie.TryWrite("a", 1));
        Assert.True(trie.TryWrite("b", 2));

        Assert.True(trie.TryRead("a", out long v1));
        Assert.Equal(1, v1);

        Assert.True(trie.TryRead("b", out long v2));
        Assert.Equal(2, v2);
    }

    [Fact]
    public void MultipleKeys_SharedPrefix()
    {
        var trie = new FlatTrie();

        Assert.True(trie.TryWrite("hello", 1));
        Assert.True(trie.TryWrite("help", 2));

        Assert.True(trie.TryRead("hello", out long v1));
        Assert.Equal(1, v1);

        Assert.True(trie.TryRead("help", out long v2));
        Assert.Equal(2, v2);
    }

    [Fact]
    public void MultipleKeys_OneIsPrefixOfOther()
    {
        var trie = new FlatTrie();

        Assert.True(trie.TryWrite("hello", 1));
        Assert.True(trie.TryWrite("helloworld", 2));

        Assert.True(trie.TryRead("hello", out long v1));
        Assert.Equal(1, v1);

        Assert.True(trie.TryRead("helloworld", out long v2));
        Assert.Equal(2, v2);
    }

    [Fact]
    public void MultipleKeys_OneIsPrefixOfOther_InsertOrder2()
    {
        var trie = new FlatTrie();

        // Insert longer first, then shorter
        Assert.True(trie.TryWrite("helloworld", 2));
        Assert.True(trie.TryWrite("hello", 1));

        Assert.True(trie.TryRead("hello", out long v1));
        Assert.Equal(1, v1);

        Assert.True(trie.TryRead("helloworld", out long v2));
        Assert.Equal(2, v2);
    }

    [Fact]
    public void OverwriteValue()
    {
        var trie = new FlatTrie();

        trie.TryWrite("hello", 42);
        Assert.True(trie.TryRead("hello", out long v1));
        Assert.Equal(42, v1);

        trie.TryWrite("hello", 100);
        Assert.True(trie.TryRead("hello", out long v2));
        Assert.Equal(100, v2);
    }

    [Fact]
    public void Delete_ExistingKey()
    {
        var trie = new FlatTrie();

        trie.TryWrite("hello", 42);
        Assert.True(trie.TryRead("hello", out _));

        trie.Delete("hello");
        Assert.False(trie.TryRead("hello", out _));
    }

    [Fact]
    public void Delete_NonExistentKey_NoOp()
    {
        var trie = new FlatTrie();

        trie.TryWrite("hello", 42);
        trie.Delete("world"); // Should not throw

        Assert.True(trie.TryRead("hello", out long value));
        Assert.Equal(42, value);
    }

    [Fact]
    public void Delete_DoesNotAffectOtherKeys()
    {
        var trie = new FlatTrie();

        trie.TryWrite("hello", 1);
        trie.TryWrite("help", 2);

        trie.Delete("hello");

        Assert.False(trie.TryRead("hello", out _));
        Assert.True(trie.TryRead("help", out long value));
        Assert.Equal(2, value);
    }

    [Fact]
    public void LargeValue()
    {
        var trie = new FlatTrie();

        long largeValue = long.MaxValue;
        trie.TryWrite("test", largeValue);

        Assert.True(trie.TryRead("test", out long value));
        Assert.Equal(largeValue, value);
    }

    [Fact]
    public void NegativeValue()
    {
        var trie = new FlatTrie();

        long negValue = -123456789L;
        trie.TryWrite("test", negValue);

        Assert.True(trie.TryRead("test", out long value));
        Assert.Equal(negValue, value);
    }

    [Fact]
    public void UnicodeKeys()
    {
        var trie = new FlatTrie();

        trie.TryWrite("héllo", 1);
        trie.TryWrite("日本語", 2);
        trie.TryWrite("emoji🎉", 3);

        Assert.True(trie.TryRead("héllo", out long v1));
        Assert.Equal(1, v1);

        Assert.True(trie.TryRead("日本語", out long v2));
        Assert.Equal(2, v2);

        Assert.True(trie.TryRead("emoji🎉", out long v3));
        Assert.Equal(3, v3);
    }

    [Fact]
    public void SaveAndLoad()
    {
        var trie = new FlatTrie();
        trie.TryWrite("hello", 1);
        trie.TryWrite("world", 2);

        string tempFile = Path.GetTempFileName();
        try
        {
            trie.Save(tempFile);

            var trie2 = new FlatTrie();
            trie2.Load(tempFile);

            Assert.True(trie2.TryRead("hello", out long v1));
            Assert.Equal(1, v1);

            Assert.True(trie2.TryRead("world", out long v2));
            Assert.Equal(2, v2);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void TwoSimilarKeys()
    {
        var trie = new FlatTrie();

        // Insert first key
        Assert.True(trie.TryWrite("key0000", 1));
        Assert.True(trie.TryRead("key0000", out long v1));
        Assert.Equal(1, v1);

        // Insert second key that shares most prefix
        Assert.True(trie.TryWrite("key0001", 2));

        // Both should be readable
        Assert.True(trie.TryRead("key0000", out long v1b), "key0000 not found after second insert");
        Assert.Equal(1, v1b);

        Assert.True(trie.TryRead("key0001", out long v2), "key0001 not found");
        Assert.Equal(2, v2);
    }

    [Fact]
    public void SevenKeys_Diagnostic()
    {
        var trie = new FlatTrie();

        // These are the exact keys that fail: key0000 through key0006
        string[] keys = ["key0000", "key0001", "key0002", "key0003", "key0004", "key0005", "key0006"];

        for (int i = 0; i < keys.Length; i++)
        {
            string key = keys[i];
            long value = i + 1;

            bool written = trie.TryWrite(key, value);
            Assert.True(written, $"Failed to write {key}");

            // Verify this key is readable
            bool found = trie.TryRead(key, out long readValue);
            Assert.True(found, $"Key {key} not found immediately after write (i={i})");
            Assert.Equal(value, readValue);

            // Verify all previous keys are still readable
            for (int j = 0; j < i; j++)
            {
                found = trie.TryRead(keys[j], out readValue);
                Assert.True(found, $"Key {keys[j]} not found after inserting {key}");
                Assert.Equal(j + 1, readValue);
            }
        }
    }

    [Fact]
    public void ManyKeys()
    {
        var trie = new FlatTrie();
        var random = new Random(42);
        var keys = new Dictionary<string, long>();

        // Insert many keys
        for (int i = 0; i < 100; i++)
        {
            string key = $"key{i:D4}";
            long value = random.NextInt64();
            keys[key] = value;

            bool written = trie.TryWrite(key, value);
            if (!written)
            {
                // Buffer might be full
                break;
            }

            // Verify this key is readable immediately
            bool found = trie.TryRead(key, out long readValue);
            Assert.True(found, $"Key {key} not found immediately after write (i={i})");
            Assert.Equal(value, readValue);
        }

        // Verify all inserted keys
        foreach (var kvp in keys)
        {
            bool found = trie.TryRead(kvp.Key, out long value);
            Assert.True(found, $"Key {kvp.Key} not found in final verification");
            Assert.Equal(kvp.Value, value);
        }
    }

    [Fact]
    public void GetStats_ReturnsValidStats()
    {
        var trie = new FlatTrie();
        trie.TryWrite("hello", 1);
        trie.TryWrite("help", 2);
        trie.TryWrite("world", 3);

        var stats = trie.GetStats();

        Assert.True(stats.NodeCount > 0);
        Assert.Equal(3, stats.ValueCount);
    }

    [Fact]
    public void EmptyKey_ReturnsFalse()
    {
        var trie = new FlatTrie();

        Assert.False(trie.TryWrite("", 1));
        Assert.False(trie.TryRead("", out _));
    }

    [Fact]
    public void NullKey_ReturnsFalse()
    {
        var trie = new FlatTrie();

        Assert.False(trie.TryWrite(null!, 1));
        Assert.False(trie.TryRead(null!, out _));
    }

    [Fact]
    public void Overflow_ReturnsFalse_AndLeavesTrieIntact()
    {
        var trie = new FlatTrie();
        var writtenKeys = new Dictionary<string, long>();

        // Fill the trie until it's nearly full
        // Use unique suffixes (not prefixes) to make the trie grow faster
        var fillTimer = Stopwatch.StartNew();
        for (int i = 0; i < 10000; i++)
        {
            string key = $"k{i:D6}";
            long value = i * 100;

            bool written = trie.TryWrite(key, value);
            if (!written)
            {
                // Buffer is full, this is expected
                break;
            }
            writtenKeys[key] = value;
        }
        fillTimer.Stop();

        // Ensure we wrote at least some keys
        Assert.True(writtenKeys.Count > 0, "Should have written at least some keys");

        _output.WriteLine($"Keys written: {writtenKeys.Count}");
        _output.WriteLine($"Fill time: {fillTimer.ElapsedMilliseconds} ms");

        // Try to write one more key that should fail
        string overflowKey = $"overflow_{Guid.NewGuid()}";
        bool overflowWritten = trie.TryWrite(overflowKey, 999999);
        Assert.False(overflowWritten, "TryWrite should return false when buffer would overflow");

        // Verify the overflow key is not readable
        Assert.False(trie.TryRead(overflowKey, out _), "Overflow key should not be readable");

        // Verify all previously written keys are still intact
        var readTimer = Stopwatch.StartNew();
        foreach (var kvp in writtenKeys)
        {
            bool found = trie.TryRead(kvp.Key, out long value);
            Assert.True(found, $"Key {kvp.Key} should still be readable after failed overflow write");
            Assert.Equal(kvp.Value, value);
        }
        readTimer.Stop();

        _output.WriteLine($"Read all keys time: {readTimer.ElapsedMilliseconds} ms");

        var stats = trie.GetStats();
        _output.WriteLine($"Node count: {stats.NodeCount}");
        _output.WriteLine($"Value count: {stats.ValueCount}");
        _output.WriteLine($"Dead end count: {stats.DeadEndCount}");
        _output.WriteLine($"Total prefix bits: {stats.TotalPrefixBits}");
        _output.WriteLine($"Max prefix length: {stats.MaxPrefixLength}");
    }

    [Fact]
    public void RandomOrderWrites_Performance()
    {
        var trie = new FlatTrie();

        // Generate keys 0-2340 (same count as ascending test)
        const int keyCount = 2340;
        var keys = new string[keyCount];
        for (int i = 0; i < keyCount; i++)
        {
            keys[i] = $"k{i:D6}";
        }

        // Shuffle with fixed seed for reproducibility
        var random = new Random(12345);
        for (int i = keys.Length - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (keys[i], keys[j]) = (keys[j], keys[i]);
        }

        // Write in random order
        var fillTimer = Stopwatch.StartNew();
        var writtenKeys = new Dictionary<string, long>();
        for (int i = 0; i < keys.Length; i++)
        {
            string key = keys[i];
            long value = i * 100;

            bool written = trie.TryWrite(key, value);
            if (!written)
            {
                _output.WriteLine($"Buffer full at key {i}");
                break;
            }
            writtenKeys[key] = value;
        }
        fillTimer.Stop();

        _output.WriteLine($"[RandomOrderWrites] Keys written: {writtenKeys.Count}");
        _output.WriteLine($"Fill time: {fillTimer.ElapsedMilliseconds} ms");

        // Verify all keys readable
        var readTimer = Stopwatch.StartNew();
        foreach (var kvp in writtenKeys)
        {
            bool found = trie.TryRead(kvp.Key, out long value);
            Assert.True(found, $"Key {kvp.Key} not found");
            Assert.Equal(kvp.Value, value);
        }
        readTimer.Stop();

        _output.WriteLine($"Read all keys time: {readTimer.ElapsedMilliseconds} ms");

        var stats = trie.GetStats();
        _output.WriteLine($"Node count: {stats.NodeCount}");
        _output.WriteLine($"Value count: {stats.ValueCount}");
        _output.WriteLine($"Dead end count: {stats.DeadEndCount}");
        _output.WriteLine($"Total prefix bits: {stats.TotalPrefixBits}");
        _output.WriteLine($"Max prefix length: {stats.MaxPrefixLength}");
    }
}
