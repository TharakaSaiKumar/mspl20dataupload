namespace GMPPro20DataUpload.Core.Interfaces;

public interface ICacheService
{
    /// <summary>
    /// Stores a value in the cache under the given key.
    /// Overwrites any existing entry for the same key.
    /// Key lookup is case-insensitive.
    /// Convention: compose key as collectionName:fieldPath:lookupValue.
    /// </summary>
    void Add(string key, string? value);

    /// <summary>
    /// Returns the cached value for the given key, or null if not found.
    /// </summary>
    string? Get(string key);

    /// <summary>
    /// Attempts to retrieve a cached value by key.
    /// Returns true and populates value if found.
    /// </summary>
    bool TryGet(string key, out string? value);

    /// <summary>
    /// Returns true if the given key exists in the cache.
    /// </summary>
    bool Exists(string key);

    /// <summary>
    /// Removes the entry for the given key. No-op if the key is not found.
    /// </summary>
    void Remove(string key);

    /// <summary>
    /// Removes all cached entries. Called at the start of a processing run.
    /// </summary>
    void Clear();
}
