using GMPPro20DataUpload.Core.Interfaces;
using GMPPro20DataUpload.Models;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace GMPPro20DataUpload.Core.Services;

public class ProcessingService : IProcessingService
{
    private const string RequestBasicInfoCollection = "usrRequestBasicInfo";

    private readonly ISchemaService _schemaService;
    private readonly ITemplateService _templateService;
    private readonly IExcelService _excelService;
    private readonly IMongoService _mongoService;
    private readonly IFlowService _flowService;
    private readonly ICacheService _cacheService;
    private readonly ApplicationSettings _appSettings;

    // Per-run insert counters — reset at start of each ProcessAsync call.
    private int _designationInsertSeq;
    private int _userInsertSeq;

    public ProcessingService(
        ISchemaService schemaService,
        ITemplateService templateService,
        IExcelService excelService,
        IMongoService mongoService,
        IFlowService flowService,
        ICacheService cacheService,
        ApplicationSettings appSettings)
    {
        _schemaService = schemaService;
        _templateService = templateService;
        _excelService = excelService;
        _mongoService = mongoService;
        _flowService = flowService;
        _cacheService = cacheService;
        _appSettings = appSettings;
    }

    public async Task<ProcessingContext> ProcessAsync(
        string schemaFilePath,
        string dataFilePath,
        string templateDirectory,
        string outputPath,
        string moduleCode,
        string requestPrefix,
        MongoConfiguration mongoConfig,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {   
        // -----------------------------------------------------------------------
        // Startup guards — fail fast before touching DB or Excel
        // -----------------------------------------------------------------------
        if (string.IsNullOrWhiteSpace(moduleCode))
            throw new InvalidOperationException(
                "moduleCode must not be empty. Ensure a format with a valid ModuleCode is selected.");

        if (string.IsNullOrWhiteSpace(requestPrefix))
            throw new InvalidOperationException(
                "requestPrefix must not be empty. Ensure the selected format has a RequestPrefix configured.");

        // -----------------------------------------------------------------------
        // Initialise run state
        // -----------------------------------------------------------------------
        _designationInsertSeq = 0;
        _userInsertSeq = 0;

        _cacheService.Clear();
        _flowService.Clear(new FlowContext()); // clear not used on the real context yet

        var ctx = new ProcessingContext
        {
            ExcelFilename = dataFilePath,
            ModuleCode    = moduleCode,
        };

        // Fresh flow context on ctx — clear it explicitly
        _flowService.Clear(ctx.FlowContext);

        // -----------------------------------------------------------------------
        // Phase 1 — run-level setup
        // -----------------------------------------------------------------------
        List<SchemaRow> schema    = _schemaService.LoadSchema(schemaFilePath);
        List<string> collectionOrder = _schemaService.GetCollectionOrder(schema);

        List<Dictionary<string, string>> dataRows = _excelService.ReadDataRows(dataFilePath);
        ctx.TotalRows = dataRows.Count;

        string activeStatusId = await _mongoService.GetActiveStatusIdAsync();

        // Generate requestCode using the format-configured prefix.
        int seq = await _mongoService.GetNextSequenceAsync(moduleCode);
        ctx.RequestCode = requestPrefix + FormatSequence(seq);

        // Populate the generic settings dictionary for Source=settings schema rows.
        ctx.Settings["moduleCode"]    = ctx.ModuleCode;
        ctx.Settings["requestPrefix"] = requestPrefix;

        // Build and insert usrRequestBasicInfo (once per upload)
        await SetupRequestBasicInfoAsync(schema, templateDirectory, ctx, activeStatusId);

        // -----------------------------------------------------------------------
        // Phase 2 — per-row loop (excludes usrRequestBasicInfo)
        // -----------------------------------------------------------------------
        List<string> rowCollections = collectionOrder
            .Where(c => !string.Equals(c, RequestBasicInfoCollection, StringComparison.OrdinalIgnoreCase))
            .ToList();

        for (int i = 0; i < dataRows.Count; i++)
        {
            Dictionary<string, string> dataRow = dataRows[i];

            ProcessResult result = await ProcessRowAsync(
                dataRow, schema, rowCollections, templateDirectory, ctx, activeStatusId);

            ctx.Results.Add(result);
            ctx.ProcessedRows++;
            progress.Report($"Processing row {ctx.ProcessedRows} of {ctx.TotalRows}");

            if (cancellationToken.IsCancellationRequested)
            {
                ctx.IsAborted = true;
                break;
            }
        }

        // -----------------------------------------------------------------------
        // Write output Excel
        // -----------------------------------------------------------------------
        _excelService.WriteOutputFile(dataFilePath, outputPath, ctx.Results);

        // Report completion through the progress pipe so the UI receives this
        // message in queue order — after all "Processing row N of N" messages.
        progress.Report(ctx.IsAborted ? "Processing aborted." : "Processing complete.");

        return ctx;
    }

    // =========================================================================
    // Phase 1 helpers
    // =========================================================================

    private async Task SetupRequestBasicInfoAsync(
        List<SchemaRow> schema,
        string templateDirectory,
        ProcessingContext ctx,
        string activeStatusId)
    {
        List<SchemaRow> collectionRows = _schemaService.GetRowsForCollection(schema, RequestBasicInfoCollection);
        JsonNode doc = _templateService.LoadTemplateAsNode(templateDirectory, RequestBasicInfoCollection);
        var resolved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Pre-pass: resolve source=lookup rows and apply to doc.
        List<SchemaRow> basicInfoLookupRows = collectionRows
            .Where(r => string.Equals(r.Source, "lookup", StringComparison.OrdinalIgnoreCase))
            .ToList();
        await ApplyLookupRowsAsync(basicInfoLookupRows, doc, new Dictionary<string, string>(), resolved);

        foreach (SchemaRow row in collectionRows)
        {
            if (string.Equals(row.Source, "auto", StringComparison.OrdinalIgnoreCase))
                continue; // filled post-insert

            if (string.Equals(row.Source, "lookup", StringComparison.OrdinalIgnoreCase))
                continue; // resolved in pre-pass above

            string value;
            if (string.Equals(row.Source, "formula", StringComparison.OrdinalIgnoreCase))
                value = EvaluateFormula(row, resolved, ctx);
            else if (string.Equals(row.Source, "settings", StringComparison.OrdinalIgnoreCase))
            {
                string key = row.FlowKey ?? string.Empty;
                if (ctx.Settings.TryGetValue(key, out string? sv))
                    value = sv;
                else if (row.IsMandatory)
                    throw new InvalidOperationException(
                        $"Settings resolution failed for property '{row.Property}' in collection '{row.Collection}'. " +
                        $"FlowKey '{key}' is not a recognised setting.");
                else
                    value = string.Empty;
            }
            else
                value = ComputeValue(row, resolved, ctx, activeStatusId, rowNumber: 0);
            resolved[row.Property] = value;

            if (!string.IsNullOrEmpty(row.Flow) &&
                string.Equals(row.Flow, "publish", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(row.FlowKey))
            {
                _flowService.Publish(ctx.FlowContext, row.FlowKey, value);
            }

            SetValueAtJsonPath(doc, row, value);
        }

        string basicInfoId = await _mongoService.InsertAsync(RequestBasicInfoCollection, doc.ToJsonString());

        // Post-insert: publish _id as requestId
        _flowService.Publish(ctx.FlowContext, "requestId", basicInfoId);

        // Ensure requestCode is also in flow (in case schema row didn't publish it)
        if (!_flowService.Exists(ctx.FlowContext, "requestCode"))
            _flowService.Publish(ctx.FlowContext, "requestCode", ctx.RequestCode!);
    }

    // =========================================================================
    // Phase 2 helpers
    // =========================================================================

    private async Task<ProcessResult> ProcessRowAsync(
        Dictionary<string, string> dataRow,
        List<SchemaRow> schema,
        List<string> collections,
        string templateDirectory,
        ProcessingContext ctx,
        string activeStatusId)
    {
        int rowNumber = dataRow.TryGetValue(ExcelService.RowNumberKey, out string? rnStr)
            && int.TryParse(rnStr, out int rn) ? rn : 0;

        var result = new ProcessResult { RowNumber = rowNumber };
        var resolved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var rowMessages = new List<string>();

            foreach (string collection in collections)
            {
                string? msg = await ProcessCollectionAsync(
                    collection, dataRow, schema, templateDirectory, ctx, activeStatusId, resolved, rowNumber);
                if (msg is not null)
                    rowMessages.Add(msg);
            }

            bool hasDuplicate = rowMessages.Any(
                m => string.Equals(m, "Duplicate", StringComparison.OrdinalIgnoreCase));

            result.IsSuccess = true;
            result.Status    = hasDuplicate ? "Duplicate" : "Inserted";
            result.Message   = string.Empty;

            // Capture the last newly inserted _id for ObjectId write-back.
            // Only populated for Inserted rows; Duplicate handling is out of scope.
            if (!hasDuplicate)
                result.ObjectId = rowMessages.LastOrDefault(
                    m => !string.Equals(m, "Duplicate", StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.Status    = "Failed";
            result.Message   = ex.Message;
        }

        return result;
    }

    private async Task<string?> ProcessCollectionAsync(
        string collection,
        Dictionary<string, string> dataRow,
        List<SchemaRow> schema,
        string templateDirectory,
        ProcessingContext ctx,
        string activeStatusId,
        Dictionary<string, string> resolved,
        int rowNumber)
    {
        List<SchemaRow> collectionRows = _schemaService.GetRowsForCollection(schema, collection);
        JsonNode doc = _templateService.LoadTemplateAsNode(templateDirectory, collection);

        // Determine lookup key (first source=key row)
        SchemaRow? lookupRow = GetLookupKey(collectionRows);
        string? cacheKey = null;
        bool isExisting = false;
        string? existingId = null;

        if (lookupRow is not null)
        {
            string lookupValue = dataRow.TryGetValue(lookupRow.Property, out string? lv) ? lv : string.Empty;
            cacheKey = $"{collection}:{lookupRow.JsonPath}:{lookupValue}";

            if (_cacheService.Exists(cacheKey))
            {
                existingId = _cacheService.Get(cacheKey);
                isExisting = true;
            }
            else
            {
                string? foundJson = await _mongoService.FindOneAsync(collection, lookupRow.JsonPath, lookupValue);
                if (foundJson is not null)
                {
                    existingId = ExtractId(foundJson);
                    _cacheService.Add(cacheKey, existingId);
                    isExisting = true;
                }
            }
        }

        // For new records — compute insert seq and reference numbers before field loop
        string formattedRef = string.Empty;
        string referenceNum = string.Empty;

        if (!isExisting)
        {
            int insertSeq = PeekNextInsertSeq(collection);
            formattedRef = ctx.RequestCode + "-" + FormatSequence(insertSeq);
            referenceNum = formattedRef + _appSettings.ReferenceSuffix;
        }

        // Pre-pass: resolve source=lookup rows and apply to doc.
        List<SchemaRow> lookupRows = collectionRows
            .Where(r => string.Equals(r.Source, "lookup", StringComparison.OrdinalIgnoreCase))
            .ToList();
        await ApplyLookupRowsAsync(lookupRows, doc, dataRow, resolved);

        // Fill document fields in schema order
        List<SchemaRow> autoRows = new();
        List<SchemaRow> updateRows = new();

        foreach (SchemaRow row in collectionRows)
        {
            if (string.Equals(row.Source, "auto", StringComparison.OrdinalIgnoreCase))
            {
                autoRows.Add(row);
                continue;
            }

            if (string.Equals(row.Source, "update", StringComparison.OrdinalIgnoreCase))
            {
                updateRows.Add(row);
                continue;
            }

            if (string.Equals(row.Source, "lookup", StringComparison.OrdinalIgnoreCase))
                continue; // resolved in pre-pass above

            string value = ResolveValue(row, dataRow, resolved, ctx, activeStatusId,
                                        rowNumber, formattedRef, referenceNum);
            resolved[row.Property] = value;

            if (!string.IsNullOrEmpty(row.Flow) &&
                string.Equals(row.Flow, "publish", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(row.FlowKey))
            {
                _flowService.Publish(ctx.FlowContext, row.FlowKey, value);
            }

            SetValueAtJsonPath(doc, row, value);
        }

        // Insert or reuse
        if (!isExisting)
        {
            IncrementInsertSeq(collection);

            string newId = await _mongoService.InsertAsync(collection, doc.ToJsonString());

            foreach (SchemaRow autoRow in autoRows)
            {
                resolved[autoRow.Property] = newId;
                if (!string.IsNullOrEmpty(autoRow.Flow) &&
                    string.Equals(autoRow.Flow, "publish", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrEmpty(autoRow.FlowKey))
                {
                    _flowService.Publish(ctx.FlowContext, autoRow.FlowKey, newId);
                }
            }

            if (cacheKey is not null)
                _cacheService.Add(cacheKey, newId);

            // Post-insert updates: set source=update fields on the newly inserted document.
            foreach (SchemaRow updateRow in updateRows)
            {
                await _mongoService.UpdateFieldAsync(
                    collection, newId, updateRow.JsonPath, newId, updateRow.DataType);
            }

            return newId;
        }
        else
        {
            // Existing record — publish cached _id via auto rows; no insert; no counter increment
            foreach (SchemaRow autoRow in autoRows)
            {
                resolved[autoRow.Property] = existingId ?? string.Empty;
                if (!string.IsNullOrEmpty(autoRow.Flow) &&
                    string.Equals(autoRow.Flow, "publish", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrEmpty(autoRow.FlowKey))
                {
                    _flowService.Publish(ctx.FlowContext, autoRow.FlowKey, existingId);
                }
            }

            // A Source=key row was configured: an existing record means this row is a duplicate.
            if (lookupRow is not null)
                return "Duplicate";

            // No Source=key row: reuse the existing record silently.
            return null;
        }
    }

    // =========================================================================
    // Value resolution
    // =========================================================================

    private string ResolveValue(
        SchemaRow row,
        Dictionary<string, string> dataRow,
        Dictionary<string, string> resolved,
        ProcessingContext ctx,
        string activeStatusId,
        int rowNumber,
        string formattedRef,
        string referenceNum)
    {
        if (string.Equals(row.Source, "settings", StringComparison.OrdinalIgnoreCase))
        {
            string key = row.FlowKey ?? string.Empty;
            if (ctx.Settings.TryGetValue(key, out string? settingValue))
                return settingValue;

            if (row.IsMandatory)
                throw new InvalidOperationException(
                    $"Settings resolution failed for property '{row.Property}' in collection '{row.Collection}'. " +
                    $"FlowKey '{key}' is not a recognised setting.");

            return string.Empty;
        }

        if (string.Equals(row.Source, "key", StringComparison.OrdinalIgnoreCase))
        {
            return dataRow.TryGetValue(row.Property, out string? kv) ? kv : string.Empty;
        }

        if (string.Equals(row.Source, "excel", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(row.Property, "dateofjoining", StringComparison.OrdinalIgnoreCase))
            {
                string raw = dataRow.TryGetValue(row.Property, out string? dv) ? dv.Trim() : string.Empty;
                return ConvertDateOfJoining(raw);
            }

            return dataRow.TryGetValue(row.Property, out string? ev) ? ev : string.Empty;
        }

        if (string.Equals(row.Source, "compute", StringComparison.OrdinalIgnoreCase))
        {
            // Gender: source=compute but depends on Excel column "gender".
            if (string.Equals(row.Property, "gender", StringComparison.OrdinalIgnoreCase))
            {
                string excelGender = dataRow.TryGetValue("gender", out string? gv) ? gv.Trim() : string.Empty;
                return BuildGenderObject(excelGender) ?? string.Empty;
            }

            // consume from flow
            if (!string.IsNullOrEmpty(row.Flow) &&
                string.Equals(row.Flow, "consume", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(row.FlowKey))
            {
                return _flowService.Consume(ctx.FlowContext, row.FlowKey) ?? string.Empty;
            }

            return ComputeValue(row, resolved, ctx, activeStatusId, rowNumber, formattedRef, referenceNum);
        }

        if (string.Equals(row.Source, "formula", StringComparison.OrdinalIgnoreCase))
            return EvaluateFormula(row, resolved, ctx);

        return string.Empty;
    }

    private string ComputeValue(
        SchemaRow row,
        Dictionary<string, string> resolved,
        ProcessingContext ctx,
        string activeStatusId,
        int rowNumber,
        string formattedRef = "",
        string referenceNum = "")
    {
        string prop = row.Property.ToLowerInvariant();

        return prop switch
        {
            "requestcode"             => ctx.RequestCode ?? string.Empty,
            "modulecode"              => ctx.ModuleCode,
            "statusid"                => activeStatusId,
            "createdon"               => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            "updatedon"               => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            "excelfilename"           => Path.GetFileName(ctx.ExcelFilename),
            "rownumber"               => rowNumber.ToString(),
            "formattedreferenceumber" => formattedRef,
            "formattedreferencenumber"=> formattedRef,
            "referencenumber"         => referenceNum,
            "formattedname"           => BuildFormattedName(row.Collection, resolved),
            _                         => string.Empty,
        };
    }

    private static string BuildFormattedName(string collection, Dictionary<string, string> resolved)    {
        if (string.Equals(collection, "masterDesignations", StringComparison.OrdinalIgnoreCase))
        {
            resolved.TryGetValue("designationName", out string? dn);
            resolved.TryGetValue("designationCode", out string? dc);
            return $"{dn} ({dc})";
        }

        if (string.Equals(collection, "masterUsers", StringComparison.OrdinalIgnoreCase))
        {
            resolved.TryGetValue("userName", out string? un);
            resolved.TryGetValue("employeeID", out string? eid);
            return $"{un} ({eid})";
        }

        return string.Empty;
    }

    // =========================================================================
    // Formula evaluation
    // =========================================================================

    /// <summary>
    /// Evaluates a Source=formula row by replacing {placeholder} tokens with
    /// resolved values.  Resolution order:
    ///   1. The per-row resolved dictionary (same-collection and prior-collection values).
    ///   2. FlowContext (system-published values such as requestCode, requestId, moduleCode).
    /// Throws InvalidOperationException if any placeholder cannot be resolved.
    /// </summary>
    private string EvaluateFormula(
        SchemaRow row,
        Dictionary<string, string> resolved,
        ProcessingContext ctx)
    {
        string formula = row.Formula ?? string.Empty;
        if (string.IsNullOrEmpty(formula))
            return string.Empty;

        return Regex.Replace(formula, @"\{(\w+)\}", match =>
        {
            string placeholder = match.Groups[1].Value;

            // 1. Per-row resolved values (covers same-collection and cross-collection prior values).
            if (resolved.TryGetValue(placeholder, out string? resolvedValue))
                return resolvedValue;

            // 2. FlowContext values (requestCode, requestId, moduleCode, and any published flow keys).
            if (_flowService.Exists(ctx.FlowContext, placeholder))
                return _flowService.Consume(ctx.FlowContext, placeholder) ?? string.Empty;

            throw new InvalidOperationException(
                $"Formula evaluation failed for property '{row.Property}' in collection '{row.Collection}'. " +
                $"Placeholder '{{{placeholder}}}' could not be resolved. " +
                $"Ensure the referenced property is defined before this row in the schema.");
        });
    }

    private static string ConvertDateOfJoining(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        DateTime dt;

        // Excel stores date cells as OADate serial numbers (e.g. "46023").
        if (double.TryParse(raw, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double serial))
        {
            dt = DateTime.FromOADate(serial);
        }
        else if (DateTime.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out dt))
        {
            // Parsed as text date — use as-is.
        }
        else
        {
            throw new InvalidOperationException(
                $"Invalid dateOfJoining value '{raw}'. Expected a valid date or an Excel date serial number.");
        }

        // Treat the parsed value as UTC (Option A: no timezone conversion).
        // Date-only input → midnight: 2026-04-27T00:00:00.000Z
        // Date-time input → preserve time: 2026-04-27T14:30:00.000Z
        return DateTime.SpecifyKind(dt, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
    }

    private static string? BuildGenderObject(string excelValue)
    {
        if (string.IsNullOrWhiteSpace(excelValue))
            return null;

        return excelValue.Trim().ToUpperInvariant() switch
        {
            "MALE" =>
                """{"itemID":"MALE","itemCode":"MALE","item":"Male","itemType":null,"isActive":null,"systemCode":null,"extraInfo":null,"displayData":"Male"}""",
            "FEMALE" =>
                """{"itemID":"FEMALE","itemCode":"FEMALE","item":"Female","itemType":null,"isActive":null,"systemCode":null,"extraInfo":null,"displayData":"Female"}""",
            _ => throw new InvalidOperationException(
                $"Unrecognised gender value '{excelValue}'. Expected: male or female.")
        };
    }

    // =========================================================================
    // Lookup resolution
    // =========================================================================

    private async Task ApplyLookupRowsAsync(
        List<SchemaRow> lookupRows,
        JsonNode doc,
        Dictionary<string, string> dataRow,
        Dictionary<string, string> resolved)
    {
        foreach (SchemaRow row in lookupRows)
        {
            string lookupKey = row.FlowKey ?? string.Empty;

            if (!_appSettings.LookupMappings.TryGetValue(lookupKey, out LookupMapping? mapping))
                throw new InvalidOperationException(
                    $"Schema row '{row.Property}' has source=lookup with FlowKey='{lookupKey}', " +
                    $"but no entry found in Application:LookupMappings.");

            // For optional excel-driven lookups, skip if the input column is absent or blank.
            // The template value at the JsonPath is retained as-is.
            if (!row.IsMandatory &&
                string.Equals(mapping.InputType, "excel", StringComparison.OrdinalIgnoreCase))
            {
                string inputColumn = mapping.InputColumn ?? string.Empty;
                bool columnMissingOrBlank =
                    !dataRow.TryGetValue(inputColumn, out string? cellValue) ||
                    string.IsNullOrWhiteSpace(cellValue);

                if (columnMissingOrBlank)
                    continue;
            }

            string foundJson = await ResolveLookupAsync(lookupKey, mapping, dataRow);

            if (string.Equals(mapping.OutputType, "objectid", StringComparison.OrdinalIgnoreCase))
            {
                string extractedValue = ExtractFieldFromJson(foundJson, mapping.OutputPath ?? "_id");
                resolved[row.Property] = extractedValue;
                SetValueAtJsonPath(doc, row, extractedValue);
            }
            else if (string.Equals(mapping.OutputType, "object", StringComparison.OrdinalIgnoreCase))
            {
                ApplyObjectMappingsToDoc(doc, row.JsonPath, foundJson, mapping.Mappings ?? new());
            }
        }
    }

    private async Task<string> ResolveLookupAsync(
        string lookupKey,
        LookupMapping mapping,
        Dictionary<string, string> dataRow)
    {
        string inputValue = string.Equals(mapping.InputType, "static", StringComparison.OrdinalIgnoreCase)
            ? (mapping.InputValue ?? string.Empty)
            : (dataRow.TryGetValue(mapping.InputColumn ?? string.Empty, out string? col) ? col.Trim() : string.Empty);

        string cacheKey = $"lookup:{lookupKey}:{inputValue}";

        if (_cacheService.TryGet(cacheKey, out string? cached) && cached is not null)
            return cached;

        string? foundJson = await _mongoService.FindOneAsync(mapping.Collection, mapping.LookupPath, inputValue);

        if (foundJson is null)
        {
            string msg = string.Equals(mapping.InputType, "static", StringComparison.OrdinalIgnoreCase)
                ? $"Lookup '{lookupKey}' configuration error. No record found in {mapping.Collection} where {mapping.LookupPath} = '{inputValue}'."
                : BuildLookupFailedMessage(lookupKey, mapping, inputValue);
            throw new InvalidOperationException(msg);
        }

        _cacheService.Add(cacheKey, foundJson);
        return foundJson;
    }

    private static string BuildLookupFailedMessage(string lookupKey, LookupMapping mapping, string inputValue)
    {
        string displayKey = lookupKey.Length > 0
            ? char.ToUpperInvariant(lookupKey[0]) + lookupKey[1..]
            : lookupKey;
        return $"{displayKey} lookup failed. No record found in {mapping.Collection} where {mapping.LookupPath} = '{inputValue}'.";
    }

    private static string ExtractFieldFromJson(string jsonDoc, string fieldPath)
    {
        using JsonDocument doc = JsonDocument.Parse(jsonDoc);
        string[] parts = fieldPath.Split('.');
        JsonElement current = doc.RootElement;

        foreach (string part in parts)
        {
            if (!current.TryGetProperty(part, out JsonElement next))
                return string.Empty;
            current = next;
        }

        // Handle MongoDB Extended JSON ObjectId: { "$oid": "..." }
        if (current.ValueKind == JsonValueKind.Object
            && current.TryGetProperty("$oid", out JsonElement oidEl))
            return oidEl.GetString() ?? string.Empty;

        if (current.ValueKind == JsonValueKind.String)
            return current.GetString() ?? string.Empty;

        return current.ToString();
    }

    private static JsonNode? NavigateToJsonPath(JsonNode root, string path)
    {
        string[] parts = path.Split('.');
        JsonNode? current = root;

        foreach (string part in parts)
        {
            if (current is not JsonObject obj)
                return null;
            current = obj[part];
            if (current is null)
                return null;
        }

        return current;
    }

    private static void ApplyObjectMappingsToDoc(
        JsonNode doc,
        string targetPath,
        string foundJson,
        Dictionary<string, string?> mappings)
    {
        JsonNode? targetNode = NavigateToJsonPath(doc, targetPath);
        if (targetNode is not JsonObject targetObj)
            throw new InvalidOperationException(
                $"Parent object at path '{targetPath}' was null or not an object. " +
                $"Verify the template and schema JsonPath values.");

        foreach (KeyValuePair<string, string?> entry in mappings)
        {
            string targetProp = entry.Key;
            string? sourcePath = entry.Value;

            if (sourcePath is null)
                targetObj[targetProp] = null;
            else
                targetObj[targetProp] = JsonValue.Create(ExtractFieldFromJson(foundJson, sourcePath));
        }
    }

    // =========================================================================
    // JsonPath writer
    // =========================================================================

    private static void SetValueAtJsonPath(JsonNode doc, SchemaRow row, string? value)
    {
        string jsonPath = row.JsonPath;
        string dataType = row.DataType;

        if (string.IsNullOrWhiteSpace(jsonPath) || value is null)
            return;

        // Diagnostic guard: objectid values must be a non-empty string before writing.
        if (string.Equals(dataType.Trim(), "objectid", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"ObjectId value is missing for property '{row.Property}' at JsonPath '{row.JsonPath}'. " +
                $"FlowKey='{row.FlowKey}', Flow='{row.Flow}', Source='{row.Source}'.");
        }

        string[] parts = jsonPath.Split('.');

        // Navigate to parent node
        JsonNode? current = doc;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (current is JsonObject obj)
            {
                if (!obj.ContainsKey(parts[i]))
                    obj[parts[i]] = new JsonObject();
                current = obj[parts[i]];
            }
            else
            {
                return; // cannot navigate further
            }
        }

        string leaf = parts[^1];
        if (current is not JsonObject parentObj)
            return;

        string dt = dataType.Trim().ToLowerInvariant();

        if (dt == "datetime")
        {
            // Write into {$date: ""} structure
            if (parentObj[leaf] is JsonObject dateObj)
                dateObj["$date"] = JsonValue.Create(value);
            else
                parentObj[leaf] = new JsonObject { ["$date"] = JsonValue.Create(value) };
        }
        else if (dt == "objectid")
        {
            // Write into {$oid: ""} structure
            if (parentObj[leaf] is JsonObject oidObj)
                oidObj["$oid"] = JsonValue.Create(value);
            else
                parentObj[leaf] = new JsonObject { ["$oid"] = JsonValue.Create(value) };
        }
        else if (dt == "integer")
        {
            if (int.TryParse(value, out int intVal))
                parentObj[leaf] = JsonValue.Create(intVal);
            else
                parentObj[leaf] = JsonValue.Create(value);
        }
        else if (dt == "object")
        {
            // Blank value writes explicit null; non-blank value is a pre-serialised JSON string.
            if (string.IsNullOrEmpty(value))
                parentObj[leaf] = null;
            else
                parentObj[leaf] = JsonNode.Parse(value);
        }
        else
        {
            parentObj[leaf] = JsonValue.Create(value);
        }
    }

    // =========================================================================
    // Private utilities
    // =========================================================================

    private static SchemaRow? GetLookupKey(List<SchemaRow> rows) =>
        rows.FirstOrDefault(r =>
            string.Equals(r.Source, "key", StringComparison.OrdinalIgnoreCase));

    private static string ExtractId(string jsonDoc)
    {
        using JsonDocument doc = JsonDocument.Parse(jsonDoc);
        if (doc.RootElement.TryGetProperty("_id", out JsonElement idEl))
        {
            if (idEl.ValueKind == JsonValueKind.String)
                return idEl.GetString() ?? string.Empty;

            if (idEl.TryGetProperty("$oid", out JsonElement oidEl))
                return oidEl.GetString() ?? string.Empty;
        }
        return string.Empty;
    }

    private int PeekNextInsertSeq(string collection)
    {
        if (string.Equals(collection, "masterDesignations", StringComparison.OrdinalIgnoreCase))
            return _designationInsertSeq + 1;
        if (string.Equals(collection, "masterUsers", StringComparison.OrdinalIgnoreCase))
            return _userInsertSeq + 1;
        return 1;
    }

    private void IncrementInsertSeq(string collection)
    {
        if (string.Equals(collection, "masterDesignations", StringComparison.OrdinalIgnoreCase))
            _designationInsertSeq++;
        else if (string.Equals(collection, "masterUsers", StringComparison.OrdinalIgnoreCase))
            _userInsertSeq++;
    }

    private static string FormatSequence(int n) =>
        n < 10 ? "0" + n : n.ToString();
}
