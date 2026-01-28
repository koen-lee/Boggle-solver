using Xunit;

namespace FlatTrie.Tests;

public class FlatTrieTests
{
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
}
