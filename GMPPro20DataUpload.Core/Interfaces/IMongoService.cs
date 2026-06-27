using GMPPro20DataUpload.Models;

namespace GMPPro20DataUpload.Core.Interfaces;

public interface IMongoService
{
    /// <summary>
    /// Tests whether the MongoDB server is reachable using the provided configuration.
    /// </summary>
    Task<bool> TestConnectionAsync(MongoConfiguration config);

    /// <summary>
    /// Finds a single document in the given collection where fieldPath matches value.
    /// Matching is case-insensitive.
    /// Returns the document as a JSON string, or null if not found.
    /// fieldPath uses dot-notation (e.g. designations.designationCode).
    /// </summary>
    Task<string?> FindOneAsync(string collectionName, string fieldPath, string value);

    /// <summary>
    /// Inserts the given JSON document into the specified collection.
    /// Returns the new document's _id as a string.
    /// </summary>
    Task<string> InsertAsync(string collectionName, string jsonDocument);

    /// <summary>
    /// Updates a single field on an existing document using MongoDB $set.
    /// fieldPath uses dot-notation (e.g. systemData.actualID).
    /// When dataType is "objectid", value is written as a BsonObjectId; otherwise as a string.
    /// </summary>
    Task UpdateFieldAsync(string collectionName, string documentId, string fieldPath, string value, string dataType);

    /// <summary>
    /// Retrieves the _id of the active status record from rootStatusMaster
    /// where statusCode = ACT.
    /// </summary>
    Task<string> GetActiveStatusIdAsync();

    /// <summary>
    /// Calculates the next sequence number for the given moduleCode
    /// based on existing records in usrRequestBasicInfo.
    /// </summary>
    Task<int> GetNextSequenceAsync(string moduleCode);

    /// <summary>
    /// Appends a child document to an array field on an existing parent document using a MongoDB $push.
    /// The parent document is located by its _id. The child document is parsed from childDocumentJson.
    /// If the target array field does not exist, MongoDB creates it.
    /// </summary>
    Task PushToArrayAsync(string collectionName, string documentId, string arrayPath, string childDocumentJson);
}
