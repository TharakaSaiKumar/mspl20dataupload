using GMPPro20DataUpload.Core.Interfaces;

namespace GMPPro20DataUpload.Core.Services;

public class CacheService : ICacheService
{
    private readonly Dictionary<string, string?> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    public void Add(string key, string? value)
        => _cache[key] = value;

    public string? Get(string key)
        => _cache.TryGetValue(key, out var value) ? value : null;

    public bool TryGet(string key, out string? value)
        => _cache.TryGetValue(key, out value);

    public bool Exists(string key)
        => _cache.ContainsKey(key);

    public void Remove(string key)
        => _cache.Remove(key);

    public void Clear()
        => _cache.Clear();
}
