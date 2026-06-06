using GMPPro20DataUpload.Core.Interfaces;
using GMPPro20DataUpload.Models;
using System.Text.Json;
using System.Text.Json.Nodes;

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
        MongoConfiguration mongoConfig,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
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
            ModuleCode    = _appSettings.CurrentModuleCode,
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

        // Generate requestCode
        string modulePrefix = _appSettings.GetModulePrefix();
        int seq = await _mongoService.GetNextSequenceAsync(_appSettings.CurrentModuleCode);
        ctx.RequestCode = modulePrefix + FormatSequence(seq);

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
        string outputPath = BuildOutputPath(dataFilePath);
        _excelService.WriteOutputFile(dataFilePath, outputPath, ctx.Results);

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

        foreach (SchemaRow row in collectionRows)
        {
            if (string.Equals(row.Source, "auto", StringComparison.OrdinalIgnoreCase))
                continue; // filled post-insert

            string value = ComputeValue(row, resolved, ctx, activeStatusId, rowNumber: 0);
            resolved[row.Property] = value;

            if (!string.IsNullOrEmpty(row.Flow) &&
                string.Equals(row.Flow, "publish", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(row.FlowKey))
            {
                _flowService.Publish(ctx.FlowContext, row.FlowKey, value);
            }

            SetValueAtJsonPath(doc, row.JsonPath, value, row.DataType);
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
            foreach (string collection in collections)
            {
                await ProcessCollectionAsync(
                    collection, dataRow, schema, templateDirectory, ctx, activeStatusId, resolved, rowNumber);
            }

            result.IsSuccess = true;
            result.Status    = "Success";
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.Status    = "Failed";
            result.Message   = ex.Message;
        }

        return result;
    }

    private async Task ProcessCollectionAsync(
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

        // Determine lookup key (first source=excel row)
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

        // Fill document fields in schema order
        List<SchemaRow> autoRows = new();

        foreach (SchemaRow row in collectionRows)
        {
            if (string.Equals(row.Source, "auto", StringComparison.OrdinalIgnoreCase))
            {
                autoRows.Add(row);
                continue;
            }

            string value = ResolveValue(row, dataRow, resolved, ctx, activeStatusId,
                                        rowNumber, formattedRef, referenceNum);
            resolved[row.Property] = value;

            if (!string.IsNullOrEmpty(row.Flow) &&
                string.Equals(row.Flow, "publish", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(row.FlowKey))
            {
                _flowService.Publish(ctx.FlowContext, row.FlowKey, value);
            }

            SetValueAtJsonPath(doc, row.JsonPath, value, row.DataType);
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
        if (string.Equals(row.Source, "excel", StringComparison.OrdinalIgnoreCase))
        {
            return dataRow.TryGetValue(row.Property, out string? ev) ? ev : string.Empty;
        }

        if (string.Equals(row.Source, "compute", StringComparison.OrdinalIgnoreCase))
        {
            // consume from flow
            if (!string.IsNullOrEmpty(row.Flow) &&
                string.Equals(row.Flow, "consume", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(row.FlowKey))
            {
                return _flowService.Consume(ctx.FlowContext, row.FlowKey) ?? string.Empty;
            }

            return ComputeValue(row, resolved, ctx, activeStatusId, rowNumber, formattedRef, referenceNum);
        }

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
            "modulecode"              => _appSettings.CurrentModuleCode,
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

    private static string BuildFormattedName(string collection, Dictionary<string, string> resolved)
    {
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
    // JsonPath writer
    // =========================================================================

    private static void SetValueAtJsonPath(JsonNode doc, string jsonPath, string? value, string dataType)
    {
        if (string.IsNullOrWhiteSpace(jsonPath) || value is null)
            return;

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
            string.Equals(r.Source, "excel", StringComparison.OrdinalIgnoreCase));

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

    private static string BuildOutputPath(string dataFilePath)
    {
        string dir      = Path.GetDirectoryName(dataFilePath) ?? string.Empty;
        string nameOnly = Path.GetFileNameWithoutExtension(dataFilePath);
        string ext      = Path.GetExtension(dataFilePath);
        return Path.Combine(dir, $"{nameOnly}_processed{ext}");
    }
}
