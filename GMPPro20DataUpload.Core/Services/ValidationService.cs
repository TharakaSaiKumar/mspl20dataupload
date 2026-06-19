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

        // Check 7: gender column must exist in Data Excel (required by masterUsers gender compute logic).
        CheckGenderColumn(result, dataFilePath);

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

        // Check 8: required Excel input columns for source=lookup mappings.
        // Runs after schema is loaded so IsMandatory can be consulted — optional lookups
        // (IsMandatory = FALSE) do not require the input column to be present in the data file.
        CheckLookupInputColumns(result, dataFilePath, schema);

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
        if (!File.Exists(dataFilePath))
            return; // Check 6 already reported this error.

        try
        {
            IReadOnlyList<string> headers = _excelService.GetColumnHeaders(dataFilePath);
            bool hasGender = headers.Any(h => string.Equals(h, "gender", StringComparison.OrdinalIgnoreCase));
            if (!hasGender)
                Fail(result, "Data file is missing required column 'gender'. The column must exist even if all values are blank.");
        }
        catch (Exception ex)
        {
            Fail(result, $"Could not read column headers from data file: {ex.Message}");
        }
    }

    private void CheckLookupInputColumns(ValidationResult result, string dataFilePath, List<SchemaRow> schema)
    {
        if (!File.Exists(dataFilePath))
            return; // Check 6 already reported this error.

        try
        {
            IReadOnlyList<string> headers = _excelService.GetColumnHeaders(dataFilePath);

            foreach (KeyValuePair<string, LookupMapping> entry in _appSettings.LookupMappings)
            {
                if (!string.Equals(entry.Value.InputType, "excel", StringComparison.OrdinalIgnoreCase))
                    continue;

                string column = entry.Value.InputColumn ?? string.Empty;
                if (string.IsNullOrEmpty(column))
                    continue;

                // If the schema row for this lookup key is optional, the input column
                // is not required to be present in the data file.
                bool isOptional = schema.Any(r =>
                    string.Equals(r.Source, "lookup", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(r.FlowKey, entry.Key, StringComparison.OrdinalIgnoreCase) &&
                    !r.IsMandatory);

                if (isOptional)
                    continue;

                bool hasColumn = headers.Any(h => string.Equals(h, column, StringComparison.OrdinalIgnoreCase));
                if (!hasColumn)
                    Fail(result, $"Data file is missing required column '{column}' (required by lookup '{entry.Key}').");
            }
        }
        catch (Exception ex)
        {
            Fail(result, $"Could not read column headers for lookup column validation: {ex.Message}");
        }
    }

    private static void Fail(ValidationResult result, string message)
    {
        result.IsValid = false;
        result.Errors.Add(message);
    }
}
