using GMPPro20DataUpload.Core.Interfaces;
using GMPPro20DataUpload.Models;

namespace GMPPro20DataUpload.Core.Services;

public class ValidationService : IValidationService
{
    private readonly IMongoService _mongoService;
    private readonly ISchemaService _schemaService;
    private readonly ITemplateService _templateService;
    private readonly IExcelService _excelService;
    private readonly ApplicationSettings _appSettings;

    public ValidationService(
        IMongoService mongoService,
        ISchemaService schemaService,
        ITemplateService templateService,
        IExcelService excelService,
        ApplicationSettings appSettings)
    {
        _mongoService = mongoService;
        _schemaService = schemaService;
        _templateService = templateService;
        _excelService = excelService;
        _appSettings = appSettings;
    }

    public async Task<ValidationResult> ValidateAsync(
        string schemaFilePath,
        string dataFilePath,
        string templateDirectory,
        MongoConfiguration mongoConfig)
    {
        var result = new ValidationResult { IsValid = true };

        // Check 1: MongoDB connection — always attempted, independent of other checks.
        await CheckMongoAsync(result, mongoConfig);

        // Check 6: Data Excel existence and readability — always attempted, independent of schema.
        CheckDataExcel(result, dataFilePath);

        // Check 2: Schema file exists on disk.
        if (!CheckSchemaFileExists(result, schemaFilePath))
            return result; // Checks 3–5 cannot run without the file.

        // Check 3: Schema file is readable and structurally valid.
        List<SchemaRow>? schema = LoadSchema(result, schemaFilePath);
        if (schema is null)
            return result; // Checks 4–5 cannot run without a parsed schema.

        // Check 4: Schema contains at least one row.
        if (!CheckSchemaNotEmpty(result, schema))
            return result; // Check 5 cannot run without collections.

        // Check 5: Template file exists for every collection referenced in the schema.
        CheckTemplates(result, schema, templateDirectory);

        // Check 7: Mandatory source columns must exist in the data file.
        // Covers Source=excel, Source=key, Source=filter (by Property name) and
        // Source=lookup (by LookupMapping.InputColumn) where IsMandatory=TRUE.
        CheckMandatoryInputColumns(result, dataFilePath, schema);

        return result;
    }

    // -------------------------------------------------------------------------
    // Private check methods — each adds to result.Errors and sets IsValid=false
    // on failure. Return value indicates whether the check passed.
    // -------------------------------------------------------------------------

    private async Task CheckMongoAsync(ValidationResult result, MongoConfiguration mongoConfig)
    {
        try
        {
            bool reachable = await _mongoService.TestConnectionAsync(mongoConfig);
            if (!reachable)
                Fail(result, "MongoDB connection failed. Verify the connection string and that the server is reachable.");
        }
        catch (Exception ex)
        {
            Fail(result, $"MongoDB connection failed. Detail: {ex.Message}");
        }
    }

    private void CheckDataExcel(ValidationResult result, string dataFilePath)
    {
        if (!File.Exists(dataFilePath))
        {
            Fail(result, $"Data file not found: {dataFilePath}");
            return;
        }

        try
        {
            _excelService.ReadDataRows(dataFilePath);
        }
        catch (Exception ex)
        {
            Fail(result, $"Data file could not be read: {dataFilePath}. Detail: {ex.Message}");
        }
    }

    private static bool CheckSchemaFileExists(ValidationResult result, string schemaFilePath)
    {
        if (File.Exists(schemaFilePath))
            return true;

        Fail(result, $"Schema file not found: {schemaFilePath}");
        return false;
    }

    private List<SchemaRow>? LoadSchema(ValidationResult result, string schemaFilePath)
    {
        try
        {
            return _schemaService.LoadSchema(schemaFilePath);
        }
        catch (Exception ex)
        {
            Fail(result, $"Schema file is invalid: {ex.Message}");
            return null;
        }
    }

    private static bool CheckSchemaNotEmpty(ValidationResult result, List<SchemaRow> schema)
    {
        if (schema.Count > 0)
            return true;

        Fail(result, "Schema file contains no valid rows after parsing.");
        return false;
    }

    private void CheckTemplates(ValidationResult result, List<SchemaRow> schema, string templateDirectory)
    {
        List<string> collections = _schemaService.GetCollectionOrder(schema);

        foreach (string collection in collections)
        {
            if (!_templateService.TemplateExists(templateDirectory, collection))
                Fail(result, $"Template file not found for collection '{collection}' in directory '{templateDirectory}'.");
        }
    }

    private void CheckGenderColumn(ValidationResult result, string dataFilePath)
    {
        // Intentionally removed — format-specific validation does not belong here.
        // Gender column presence is now validated generically via CheckMandatoryInputColumns
        // when the schema defines the gender property with IsMandatory=TRUE and Source=excel.
    }

    private void CheckMandatoryInputColumns(ValidationResult result, string dataFilePath, List<SchemaRow> schema)
    {
        if (!File.Exists(dataFilePath))
            return; // Check 2 already reported this error.

        try
        {
            IReadOnlyList<string> headers = _excelService.GetColumnHeaders(dataFilePath);

            // Source=excel, Source=key, Source=filter: the row.Property name IS the data column.
            var directSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "excel", "key", "filter" };

            foreach (SchemaRow row in schema)
            {
                if (!row.IsMandatory || !directSources.Contains(row.Source))
                    continue;

                bool hasColumn = headers.Any(h => string.Equals(h, row.Property, StringComparison.OrdinalIgnoreCase));
                if (!hasColumn)
                    Fail(result,
                        $"Collection={row.Collection}, Property={row.Property}, Source={row.Source}: " +
                        $"Required data column '{row.Property}' is missing from the data file.");
            }

            // Source=lookup: the input column is specified in LookupMappings (InputType=excel only).
            foreach (SchemaRow row in schema)
            {
                if (!row.IsMandatory ||
                    !string.Equals(row.Source, "lookup", StringComparison.OrdinalIgnoreCase))
                    continue;

                string lookupKey = row.FlowKey ?? string.Empty;
                if (string.IsNullOrEmpty(lookupKey))
                    continue;

                if (!_appSettings.LookupMappings.TryGetValue(lookupKey, out LookupMapping? mapping))
                    continue; // Missing mapping is caught by SchemaService validation.

                // Validate MSSQL lookup provider configuration.
                if (string.Equals(mapping.LookupProvider, "mssql", StringComparison.OrdinalIgnoreCase))
                {
                    string connName = mapping.ConnectionName ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(connName))
                    {
                        Fail(result,
                            $"Collection={row.Collection}, Property={row.Property}, Source={row.Source}: " +
                            $"Lookup '{lookupKey}' uses LookupProvider=mssql but ConnectionName is not configured.");
                        continue;
                    }

                    if (!_appSettings.ConnectionStrings.TryGetValue(connName, out string? connStr)
                        || string.IsNullOrWhiteSpace(connStr))
                    {
                        Fail(result,
                            $"Collection={row.Collection}, Property={row.Property}, Source={row.Source}: " +
                            $"Lookup '{lookupKey}' references ConnectionName='{connName}' which is not found in ConnectionStrings configuration.");
                    }

                    continue; // MSSQL lookups do not use an InputColumn from the data file.
                }

                if (!string.Equals(mapping.InputType, "excel", StringComparison.OrdinalIgnoreCase))
                    continue; // Static lookups don't require a data column.

                string column = mapping.InputColumn ?? string.Empty;
                if (string.IsNullOrEmpty(column))
                    continue;

                bool hasColumn = headers.Any(h => string.Equals(h, column, StringComparison.OrdinalIgnoreCase));
                if (!hasColumn)
                    Fail(result,
                        $"Collection={row.Collection}, Property={row.Property}, Source={row.Source}: " +
                        $"Required data column '{column}' (lookup '{lookupKey}') is missing from the data file.");
            }
        }
        catch (Exception ex)
        {
            Fail(result, $"Could not read column headers for mandatory column validation: {ex.Message}");
        }
    }

    private static void Fail(ValidationResult result, string message)
    {
        result.IsValid = false;
        result.Errors.Add(message);
    }
}
