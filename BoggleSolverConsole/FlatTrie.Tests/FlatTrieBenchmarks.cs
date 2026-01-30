using BenchmarkDotNet.Attributes;

namespace FlatTrie.Tests;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class FlatTrieBenchmarks
{
    private string[] _randomOrderKeys = null!;
    private string[] _sequentialKeys = null!;
    private string[] _guidKeys = null!;
    private FlatTrie _prefilledTrie = null!;
    private string[] _fileKeys = null!;
    private Dictionary<string, long> _prefilledValues = null!;
    private FlatTrie _prefilledGuidTrie = null!;
    private Dictionary<string, long> _prefilledGuidValues = null!;
    private FlatTrie _prefilledFileTrie = null!;
    private Dictionary<string, long> _prefilledFileValues = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Setup for RandomOrderWrites - same as the test
        const int keyCount = 2340;
        _randomOrderKeys = new string[keyCount];
        _sequentialKeys = new string[keyCount];
        _guidKeys = new string[keyCount];
        for (int i = 0; i < keyCount; i++)
        {
            _randomOrderKeys[i] = $"k{i:D6}";
            _sequentialKeys[i] = $"k{i:D6}";
            _guidKeys[i] = Guid.NewGuid().ToString("N");
        }

        // Shuffle with fixed seed for reproducibility
        var random = new Random(12345);
        for (int i = _randomOrderKeys.Length - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (_randomOrderKeys[i], _randomOrderKeys[j]) = (_randomOrderKeys[j], _randomOrderKeys[i]);
        }

        // Pre-fill a trie for read benchmarks
        _prefilledTrie = new FlatTrie();
        _prefilledValues = new Dictionary<string, long>();
        for (int i = 0; i < _sequentialKeys.Length; i++)
        {
            string key = _sequentialKeys[i];
            long value = i * 100;
            if (_prefilledTrie.TryWrite(key, value))
            {
                _prefilledValues[key] = value;
            }
        }

        // Pre-fill a trie with Guids for read benchmarks with longer, random keys
        _prefilledGuidTrie = new FlatTrie();
        _prefilledGuidValues = new Dictionary<string, long>();
        for (int i = 0; i < _guidKeys.Length; i++)
        {
            string key = _guidKeys[i];
            long value = i * 100;
            if (_prefilledGuidTrie.TryWrite(key, value))
            {
                _prefilledGuidValues[key] = value;
            }
        }

        
        // Pre-fill a trie with Guids for read benchmarks with longer, random keys
        _prefilledFileTrie = new FlatTrie();
        _prefilledFileValues = new Dictionary<string, long>();
        
        _fileKeys = File.ReadAllLines("FileList.txt");
        for (int i = 0; i < _fileKeys.Length; i++)
        {
            string key = _fileKeys[i];
            long value = i * 100;
            if (_prefilledFileTrie.TryWrite(key, value))
            {
                _prefilledFileValues[key] = value;
            }
        }
    }

    [Benchmark]
    public int RandomOrderWrites()
    {
        var trie = new FlatTrie();
        int written = 0;

        for (int i = 0; i < _randomOrderKeys.Length; i++)
        {
            if (trie.TryWrite(_randomOrderKeys[i], i * 100))
                written++;
        }

        return written;
    }

    
    [Benchmark]
    public int RandomOrderWrites_Dictionary()
    {
        var dict = new Dictionary<string, long>();
        int written = 0;

        for (int i = 0; i < _randomOrderKeys.Length; i++)
        {
            if (dict.TryAdd(_randomOrderKeys[i], i * 100))
                written++;
        }

        return written;
    }

    [Benchmark]
    public int SequentialWrites()
    {
        var trie = new FlatTrie();
        int written = 0;

        for (int i = 0; i < _sequentialKeys.Length; i++)
        {
            if (trie.TryWrite(_sequentialKeys[i], i * 100))
                written++;
        }

        return written;
    }

    [Benchmark]
    public int GuidWrites()
    {
        var trie = new FlatTrie();
        int written = 0;

        for (int i = 0; i < _guidKeys.Length; i++)
        {
            if (trie.TryWrite(_guidKeys[i], i * 100))
                written++;
        }

        return written;
    }

    [Benchmark]
    public int FileWrites()
    {
        var trie = new FlatTrie();
        int written = 0;

        for (int i = 0; i < _fileKeys.Length; i++)
        {
            if (trie.TryWrite(_fileKeys[i], i * 100))
                written++;
        }

        return written;
    }

    [Benchmark]
    public int ReadAllKeys()
    {
        int found = 0;
        foreach (var key in _prefilledValues.Keys)
        {
            if (_prefilledTrie.TryRead(key, out _))
                found++;
        }
        return found;
    }

    [Benchmark]
    public int ReadAllKeys_Dictionary()
    {
        int found = 0;
        foreach (var key in _randomOrderKeys)
        {
            if (_prefilledValues.TryGetValue(key, out _))
                found++;
        }
        return found;
    }

    [Benchmark]
    public int ReadAllGuidKeys()
    {
        int found = 0;
        foreach (var key in _prefilledGuidValues.Keys)
        {
            if (_prefilledGuidTrie.TryRead(key, out _))
                found++;
        }
        return found;
    }

    [Benchmark]
    public int ReadAllFileKeys()
    {
        int found = 0;
        foreach (var key in _prefilledFileValues.Keys)
        {
            if (_prefilledFileTrie.TryRead(key, out _) )
                found++;
        }
        return found;
    }

    [Benchmark]
    public (int written, int read) OverflowScenario()
    {
        var trie = new FlatTrie();
        var writtenKeys = new Dictionary<string, long>();

        // Fill until overflow (same as Overflow_ReturnsFalse_AndLeavesTrieIntact)
        for (int i = 0; i < 10000; i++)
        {
            string key = $"k{i:D6}";
            long value = i * 100;

            if (!trie.TryWrite(key, value))
                break;

            writtenKeys[key] = value;
        }

        // Read all back
        int readCount = 0;
        foreach (var kvp in writtenKeys)
        {
            if (trie.TryRead(kvp.Key, out long value) && value == kvp.Value)
                readCount++;
        }

        return (writtenKeys.Count, readCount);
    }
}
