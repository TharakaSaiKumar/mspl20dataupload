using GMPPro20DataUpload.Core.Interfaces;
using GMPPro20DataUpload.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.RegularExpressions;

namespace GMPPro20DataUpload.Core.Services;

public class MongoService : IMongoService
{
    private const string RootStatusMasterCollection = "rootStatusMaster";
    private const string RequestInfoCollection = "usrRequestBasicInfo";

    private readonly MongoConfiguration _config;

    private MongoClient? _client;
    private IMongoDatabase? _database;

    // Cached after first successful retrieval — safe because MongoService is Singleton.
    private string? _activeStatusId;

    public MongoService(MongoConfiguration config)
    {
        _config = config;
    }

    // -------------------------------------------------------------------------
    // IMongoService
    // -------------------------------------------------------------------------

    public async Task<bool> TestConnectionAsync(MongoConfiguration config)
    {
        // Use the supplied config for the connection test (interface compatibility).
        // Internal operations use the injected _config.
        try
        {
            var client = new MongoClient(config.ConnectionString);
            var db = client.GetDatabase(config.DatabaseName);
            await db.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string?> FindOneAsync(string collectionName, string fieldPath, string value)
    {
        var collection = GetDatabase().GetCollection<BsonDocument>(collectionName);

        // Case-insensitive exact match via anchored regex.
        var filter = Builders<BsonDocument>.Filter.Regex(
            fieldPath,
            new BsonRegularExpression($"^{Regex.Escape(value)}$", "i"));

        BsonDocument? doc = await collection.Find(filter).FirstOrDefaultAsync();
        return doc?.ToJson();
    }

    public async Task<string> InsertAsync(string collectionName, string jsonDocument)
    {
        var collection = GetDatabase().GetCollection<BsonDocument>(collectionName);
        BsonDocument doc = BsonDocument.Parse(jsonDocument);

        await collection.InsertOneAsync(doc);

        return doc["_id"].ToString()!;
    }

    public async Task UpdateFieldAsync(
        string collectionName,
        string documentId,
        string fieldPath,
        string value,
        string dataType)
    {
        var collection = GetDatabase().GetCollection<BsonDocument>(collectionName);

        FilterDefinition<BsonDocument> filter =
            Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(documentId));

        BsonValue bsonValue = string.Equals(dataType.Trim(), "objectid", StringComparison.OrdinalIgnoreCase)
            ? new BsonObjectId(ObjectId.Parse(value))
            : new BsonString(value);

        UpdateDefinition<BsonDocument> update =
            Builders<BsonDocument>.Update.Set(fieldPath, bsonValue);

        await collection.UpdateOneAsync(filter, update);
    }

    public async Task<string> GetActiveStatusIdAsync()
    {
        if (_activeStatusId is not null)
            return _activeStatusId;

        var collection = GetDatabase().GetCollection<BsonDocument>(RootStatusMasterCollection);

        var filter = Builders<BsonDocument>.Filter.Regex(
            "statusCode",
            new BsonRegularExpression("^ACT$", "i"));

        BsonDocument doc = await collection.Find(filter).FirstOrDefaultAsync()
            ?? throw new InvalidOperationException(
                "No active status record found in rootStatusMaster where statusCode = ACT.");

        _activeStatusId = doc["_id"].ToString()!;
        return _activeStatusId;
    }

    public async Task<int> GetNextSequenceAsync(string moduleCode)
    {
        var collection = GetDatabase().GetCollection<BsonDocument>(RequestInfoCollection);

        // Count documents where moduleCode field equals the given moduleCode (case-insensitive).
        var filter = Builders<BsonDocument>.Filter.Regex(
            "moduleCode",
            new BsonRegularExpression($"^{Regex.Escape(moduleCode)}$", "i"));

        long count = await collection.CountDocumentsAsync(filter);

        return (int)count + 1;
    }

    public async Task PushToArrayAsync(string collectionName, string documentId, string arrayPath, string childDocumentJson)
    {
        var collection = GetDatabase().GetCollection<BsonDocument>(collectionName);

        FilterDefinition<BsonDocument> filter =
            Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(documentId));

        BsonDocument childDoc = BsonDocument.Parse(childDocumentJson);

        // If target array exists as null, convert it to empty array first.
        var nullArrayFilter = filter & Builders<BsonDocument>.Filter.Eq(arrayPath, BsonNull.Value);
        var setEmptyArray = Builders<BsonDocument>.Update.Set(arrayPath, new BsonArray());

        await collection.UpdateOneAsync(nullArrayFilter, setEmptyArray);

        // Push generated object into target array.
        UpdateDefinition<BsonDocument> update =
            Builders<BsonDocument>.Update.Push(arrayPath, childDoc);

        await collection.UpdateOneAsync(filter, update);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private IMongoDatabase GetDatabase()
    {
        if (_database is not null)
            return _database;

        _client = new MongoClient(_config.ConnectionString);
        _database = _client.GetDatabase(_config.DatabaseName);
        return _database;
    }
}
