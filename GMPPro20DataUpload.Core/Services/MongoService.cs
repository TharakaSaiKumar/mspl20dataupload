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

        // Retrieve all requestCode values where requestCode starts with moduleCode (case-insensitive).
        var filter = Builders<BsonDocument>.Filter.Regex(
            "requestCode",
            new BsonRegularExpression($"^{Regex.Escape(moduleCode)}", "i"));

        var projection = Builders<BsonDocument>.Projection
            .Include("requestCode")
            .Exclude("_id");

        List<BsonDocument> docs = await collection.Find(filter).Project(projection).ToListAsync();

        int maxSeq = 0;
        int prefixLen = moduleCode.Length;

        foreach (BsonDocument doc in docs)
        {
            if (!doc.TryGetValue("requestCode", out BsonValue? rcValue))
                continue;

            string rc = rcValue.AsString;
            if (rc.Length <= prefixLen)
                continue;

            string suffix = rc[prefixLen..];
            if (int.TryParse(suffix, out int seq) && seq > maxSeq)
                maxSeq = seq;
        }

        return maxSeq + 1;
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
