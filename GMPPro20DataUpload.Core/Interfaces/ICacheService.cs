namespace GMPPro20DataUpload.Core.Interfaces;

public interface ICacheService
{
    /// <summary>
    /// Attempts to retrieve a cached value by key.
    /// Returns true and populates value if found.
    /// Key lookup is case-insensitive.
    /// Convention: compose key as collectionName + fieldPath + lookupValue.
    /// </summary>
    bool TryGet(string key, out string? value);

    /// <summary>
    /// Stores a value in the cache under the given key.
    /// </summary>
    void Set(string key, string? value);

    /// <summary>
    /// Clears all cached entries. Called between processing runs.
    /// </summary>
    void Clear();
}
