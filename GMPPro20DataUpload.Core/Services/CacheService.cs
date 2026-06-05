using GMPPro20DataUpload.Core.Interfaces;

namespace GMPPro20DataUpload.Core.Services;

public class CacheService : ICacheService
{
    public bool TryGet(string key, out string? value)
        => throw new NotImplementedException();

    public void Set(string key, string? value)
        => throw new NotImplementedException();

    public void Clear()
        => throw new NotImplementedException();
}
