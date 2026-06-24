using GMPPro20DataUpload.Core.Interfaces;
using GMPPro20DataUpload.Models;
using System.Text;

namespace GMPPro20DataUpload.Core.Services;

public class SchemaService : ISchemaService
{
    private static readonly HashSet<string> ValidDataTypes =
        new(StringComparer.OrdinalIgnoreCase) { "text", "integer", "datetime", "objectid", "object" };

    private static readonly HashSet<string> ValidSources =
        new(StringComparer.OrdinalIgnoreCase) { "excel", "compute", "auto", "update", "lookup", "settings" };

    private static readonly HashSet<string> ValidFlowActions =
        new(StringComparer.OrdinalIgnoreCase) { "publish", "consume" };

    private readonly IExcelService _excelService;

    public SchemaService(IExcelService excelService)
    {
        _excelService = excelService;
    }

    public List<SchemaRow> LoadSchema(string schemaFilePath)
    {
        List<SchemaRow> rows = _excelService.ReadSchemaRows(schemaFilePath);

        // Skip blank rows (Collection is empty after trimming is done by ExcelService)
        List<SchemaRow> nonBlank = rows
            .Where(r => !string.IsNullOrWhiteSpace(r.Collection))
            .ToList();

        ValidateSchema(nonBlank);

        return nonBlank;
    }

    public List<SchemaRow> GetRowsForCollection(List<SchemaRow> schema, string collectionName) =>
        schema
            .Where(r => string.Equals(r.Collection, collectionName, StringComparison.OrdinalIgnoreCase))
            .ToList();

    public List<string> GetCollectionOrder(List<SchemaRow> schema)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();

        foreach (SchemaRow row in schema)
        {
            if (seen.Add(row.Collection))
                order.Add(row.Collection);
        }

        return order;
    }

    public List<SchemaRow> GetRowsBySource(List<SchemaRow> rows, string source) =>
        rows
            .Where(r => string.Equals(r.Source, source, StringComparison.OrdinalIgnoreCase))
            .ToList();

    public List<SchemaRow> GetFlowRows(List<SchemaRow> rows, string flowAction) =>
        rows
            .Where(r => string.Equals(r.Flow, flowAction, StringComparison.OrdinalIgnoreCase))
            .ToList();

    // -------------------------------------------------------------------------
    // Private — structural validation
    // -------------------------------------------------------------------------

    private static void ValidateSchema(List<SchemaRow> rows)
    {
        var errors = new StringBuilder();
        int errorCount = 0;

        for (int i = 0; i < rows.Count; i++)
        {
            SchemaRow row = rows[i];
            // Row number reported as 1-based data row index (header is row 1)
            string loc = $"Row {i + 2} (Collection={row.Collection}, Property={row.Property})";

            if (string.IsNullOrWhiteSpace(row.Property))
                errors.AppendLine($"{loc}: Property must not be empty.");

            if (string.IsNullOrWhiteSpace(row.DataType) || !ValidDataTypes.Contains(row.DataType))
                errors.AppendLine($"{loc}: DataType '{row.DataType}' is invalid. Expected: text | integer | datetime | objectid.");

            if (string.IsNullOrWhiteSpace(row.Source) || !ValidSources.Contains(row.Source))
                errors.AppendLine($"{loc}: Source '{row.Source}' is invalid. Expected: excel | compute | auto.");

            if (string.IsNullOrWhiteSpace(row.JsonPath))
                errors.AppendLine($"{loc}: JsonPath must not be empty.");

            bool hasFlow = !string.IsNullOrWhiteSpace(row.Flow);

            if (hasFlow && !ValidFlowActions.Contains(row.Flow!))
                errors.AppendLine($"{loc}: Flow '{row.Flow}' is invalid. Expected: publish | consume.");

            if (hasFlow && string.IsNullOrWhiteSpace(row.FlowKey))
                errors.AppendLine($"{loc}: FlowKey must not be empty when Flow is '{row.Flow}'.");

            if (string.Equals(row.Source, "settings", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(row.FlowKey))
                errors.AppendLine($"{loc}: FlowKey must not be empty when Source is 'settings'.");

            errorCount++;
        }

        if (errors.Length > 0)
            throw new InvalidOperationException(
                $"Schema validation failed with errors:\n{errors}");
    }
}
