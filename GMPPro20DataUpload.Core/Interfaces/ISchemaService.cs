using GMPPro20DataUpload.Models;

namespace GMPPro20DataUpload.Core.Interfaces;

public interface ISchemaService
{
    /// <summary>
    /// Loads all schema rows from the given Schema Excel file.
    /// Row order is preserved exactly as it appears in the file.
    /// Blank rows are skipped. Structural validation is applied.
    /// Throws InvalidOperationException if structural violations are found.
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

    /// <summary>
    /// Returns rows from the given list where Source matches the specified value.
    /// Comparison is case-insensitive.
    /// Valid values: excel | compute | auto
    /// </summary>
    List<SchemaRow> GetRowsBySource(List<SchemaRow> rows, string source);

    /// <summary>
    /// Returns rows from the given list where Flow matches the specified action.
    /// Comparison is case-insensitive.
    /// Valid values: publish | consume
    /// </summary>
    List<SchemaRow> GetFlowRows(List<SchemaRow> rows, string flowAction);
}
