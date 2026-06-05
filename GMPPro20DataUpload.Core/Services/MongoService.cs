using GMPPro20DataUpload.Core.Interfaces;
using GMPPro20DataUpload.Models;

namespace GMPPro20DataUpload.Core.Services;

public class MongoService : IMongoService
{
    public Task<bool> TestConnectionAsync(MongoConfiguration config)
        => throw new NotImplementedException();

    public Task<string?> FindOneAsync(string collectionName, string fieldPath, string value)
        => throw new NotImplementedException();

    public Task<string> InsertAsync(string collectionName, string jsonDocument)
        => throw new NotImplementedException();

    public Task<string> GetActiveStatusIdAsync()
        => throw new NotImplementedException();

    public Task<int> GetNextSequenceAsync(string moduleCode)
        => throw new NotImplementedException();
}
