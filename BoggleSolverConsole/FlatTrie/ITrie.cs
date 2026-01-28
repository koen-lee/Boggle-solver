namespace FlatTrie;

/// <summary>
/// A trie that stores string keys with long values.
/// Backed by a fixed-size byte array with no external state.
/// </summary>
public interface ITrie
{
    /// <summary>
    /// Try to add the key/value to the trie.
    /// Can fail if there isn't enough room.
    /// If key already exists, will overwrite old value.
    /// </summary>
    bool TryWrite(string key, long value);

    /// <summary>
    /// Try to find the key in the trie.
    /// If found, puts the value in the out param.
    /// Returns false if key is not found.
    /// </summary>
    bool TryRead(string key, out long value);

    /// <summary>
    /// Remove the key from the trie.
    /// No-op if the key isn't there.
    /// </summary>
    void Delete(string key);

    /// <summary>
    /// Saves the internal array to a file.
    /// </summary>
    void Save(string filename);

    /// <summary>
    /// Loads the internal array from a file.
    /// </summary>
    void Load(string filename);
}
