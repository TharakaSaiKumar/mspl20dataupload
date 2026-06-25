using GMPPro20DataUpload.Core.Interfaces;
using GMPPro20DataUpload.Models;
using System.Text;
using System.Text.RegularExpressions;

namespace GMPPro20DataUpload.Core.Services;

public class SchemaService : ISchemaService
{
    private static readonly HashSet<string> ValidDataTypes =
        new(StringComparer.OrdinalIgnoreCase) { "text", "integer", "datetime", "objectid", "object" };

    private static readonly HashSet<string> ValidSources =
        new(StringComparer.OrdinalIgnoreCase) { "excel", "compute", "auto", "update", "lookup", "settings", "key", "filter", "formula" };

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
                errors.AppendLine($"{loc}: Source '{row.Source}' is invalid. Expected: excel | compute | auto | update | lookup | settings | key | filter | formula.");

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

            if (string.Equals(row.Source, "filter", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(row.FlowKey))
                errors.AppendLine($"{loc}: FlowKey must not be empty when Source is 'filter'.");

            if (string.Equals(row.Source, "lookup", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(row.FlowKey))
                errors.AppendLine($"{loc}: FlowKey must not be empty when Source is 'lookup'.");

            if (string.Equals(row.Source, "formula", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(row.Formula))
                errors.AppendLine($"{loc}: Formula must not be empty when Source is 'formula'.");

            errorCount++;
        }

        // Formula dependency validation: all {placeholder} references in a formula must resolve
        // to a property defined before the formula row in schema order, or to a known system key.
        //
        // Reachable set for a formula row at index i in collection C:
        //   1. All properties from collections that appear before C in collection order.
        //   2. All properties from rows within C that appear before row i.
        //   3. Known system-published keys: requestCode, requestId, moduleCode.
        var systemKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "requestCode", "requestId", "moduleCode" };

        // Build collection order from the full row list.
        var collectionOrder = new List<string>();
        var seenCollections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (SchemaRow r in rows)
        {
            if (seenCollections.Add(r.Collection))
                collectionOrder.Add(r.Collection);
        }

        for (int i = 0; i < rows.Count; i++)
        {
            SchemaRow row = rows[i];
            if (!string.Equals(row.Source, "formula", StringComparison.OrdinalIgnoreCase))
                continue;
            if (string.IsNullOrWhiteSpace(row.Formula))
                continue; // Missing formula already reported above.

            string loc = $"Row {i + 2} (Collection={row.Collection}, Property={row.Property})";

            // Determine which collections precede this formula row's collection.
            int collectionIndex = collectionOrder.IndexOf(
                collectionOrder.First(c => string.Equals(c, row.Collection, StringComparison.OrdinalIgnoreCase)));
            var priorCollections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int k = 0; k < collectionIndex; k++)
                priorCollections.Add(collectionOrder[k]);

            // Build the full reachable property set.
            var reachable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            reachable.UnionWith(systemKeys);

            for (int j = 0; j < i; j++)
            {
                // Properties from earlier collections (all their rows are reachable).
                if (priorCollections.Contains(rows[j].Collection))
                    reachable.Add(rows[j].Property);

                // Properties from the same collection that appear before this row.
                if (string.Equals(rows[j].Collection, row.Collection, StringComparison.OrdinalIgnoreCase))
                    reachable.Add(rows[j].Property);
            }

            foreach (Match match in Regex.Matches(row.Formula!, @"\{(\w+)\}"))
            {
                string dependency = match.Groups[1].Value;
                if (!reachable.Contains(dependency))
                    errors.AppendLine(
                        $"{loc}: Formula dependency '{dependency}' is not defined before this row.");
            }
        }

        if (errors.Length > 0)
            throw new InvalidOperationException(
                $"Schema validation failed with errors:\n{errors}");
    }
}
