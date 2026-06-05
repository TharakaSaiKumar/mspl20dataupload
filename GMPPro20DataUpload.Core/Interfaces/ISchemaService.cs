using GMPPro20DataUpload.Models;

namespace GMPPro20DataUpload.Core.Interfaces;

public interface ISchemaService
{
    /// <summary>
    /// Loads all schema rows from the given Schema Excel file.
    /// Row order is preserved exactly as it appears in the file.
    /// </summary>
    List<SchemaRow> LoadSchema(string schemaFilePath);

    /// <summary>
    /// Returns schema rows belonging to the specified collection.
    /// Order is preserved from the source list.
    /// </summary>
    List<SchemaRow> GetRowsForCollection(List<SchemaRow> schema, string collectionName);

    /// <summary>
    /// Returns the distinct collection names in the order they first appear in the schema.
    /// This order drives the processing sequence.
    /// </summary>
    List<string> GetCollectionOrder(List<SchemaRow> schema);
}
