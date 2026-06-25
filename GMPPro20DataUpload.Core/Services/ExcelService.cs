using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using GMPPro20DataUpload.Core.Constants;
using GMPPro20DataUpload.Core.Interfaces;
using GMPPro20DataUpload.Models;

namespace GMPPro20DataUpload.Core.Services;

public class ExcelService : IExcelService
{
    /// <summary>
    /// Internal key injected into each data row dictionary to carry the actual
    /// Excel worksheet row number (1-based). Callers must not use this as a data column.
    /// </summary>
    public const string RowNumberKey = "__RowNumber__";

    // -------------------------------------------------------------------------
    // IExcelService
    // -------------------------------------------------------------------------

    public List<SchemaRow> ReadSchemaRows(string filePath)
    {
        var rows = new List<SchemaRow>();

        using SpreadsheetDocument doc = OpenReadOnly(filePath);
        WorksheetPart wsPart = GetFirstWorksheetPart(doc);
        SheetData sheetData = wsPart.Worksheet!.GetFirstChild<SheetData>()
            ?? throw new InvalidOperationException($"Schema file has no sheet data: {filePath}");

        List<string?> sst = BuildSharedStringTable(doc);
        List<Row> allRows = sheetData.Elements<Row>().ToList();

        if (allRows.Count == 0)
            return rows;

        // Row 1 — header
        Dictionary<int, string> headerMap = BuildHeaderMap(allRows[0], sst);

        // Rows 2+ — data
        foreach (Row row in allRows.Skip(1))
        {
            Dictionary<int, string> cells = ReadCells(row, sst);
            if (IsBlankRow(cells))
                continue;

            rows.Add(MapToSchemaRow(cells, headerMap));
        }

        return rows;
    }

    public List<Dictionary<string, string>> ReadDataRows(string filePath)
    {
        var rows = new List<Dictionary<string, string>>();

        using SpreadsheetDocument doc = OpenReadOnly(filePath);
        WorksheetPart wsPart = GetFirstWorksheetPart(doc);
        SheetData sheetData = wsPart.Worksheet!.GetFirstChild<SheetData>()
            ?? throw new InvalidOperationException($"Data file has no sheet data: {filePath}");

        List<string?> sst = BuildSharedStringTable(doc);
        List<Row> allRows = sheetData.Elements<Row>().ToList();

        if (allRows.Count == 0)
            return rows;

        // Row 1 — header; key = column index, value = trimmed header name
        Dictionary<int, string> headerMap = BuildHeaderMap(allRows[0], sst);

        // Rows 2+ — data
        foreach (Row row in allRows.Skip(1))
        {
            Dictionary<int, string> cells = ReadCells(row, sst);
            if (IsBlankRow(cells))
                continue;

            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Stamp actual worksheet row index (1-based uint → int)
            dict[RowNumberKey] = row.RowIndex?.ToString() ?? string.Empty;

            foreach (KeyValuePair<int, string> header in headerMap)
            {
                dict[header.Value] = cells.TryGetValue(header.Key, out string? v) ? v : string.Empty;
            }

            rows.Add(dict);
        }

        return rows;
    }

    public IReadOnlyList<string> GetColumnHeaders(string filePath)
    {
        using SpreadsheetDocument doc = OpenReadOnly(filePath);
        WorksheetPart wsPart = GetFirstWorksheetPart(doc);
        SheetData sheetData = wsPart.Worksheet!.GetFirstChild<SheetData>()
            ?? throw new InvalidOperationException($"Data file has no sheet data: {filePath}");

        List<string?> sst = BuildSharedStringTable(doc);
        Row? headerRow = sheetData.Elements<Row>().FirstOrDefault();

        if (headerRow is null)
            return Array.Empty<string>();

        Dictionary<int, string> headerMap = BuildHeaderMap(headerRow, sst);
        return headerMap.Values.ToList();
    }

    public void WriteOutputFile(string sourcePath, string destinationPath, List<ProcessResult> results)
    {
        File.Copy(sourcePath, destinationPath, overwrite: true);

        using SpreadsheetDocument doc = SpreadsheetDocument.Open(destinationPath, isEditable: true);
        WorksheetPart wsPart = GetFirstWorksheetPart(doc);
        SheetData sheetData = wsPart.Worksheet!.GetFirstChild<SheetData>()
            ?? throw new InvalidOperationException($"Output file has no sheet data: {destinationPath}");

        List<string?> sst = BuildSharedStringTable(doc);
        List<Row> allRows = sheetData.Elements<Row>().ToList();

        if (allRows.Count == 0)
            return;

        // Determine the next available column index (after existing columns in header row)
        Dictionary<int, string> headerMap = BuildHeaderMap(allRows[0], sst);
        int statusColIdx  = headerMap.Count > 0 ? headerMap.Keys.Max() + 1 : 1;
        int messageColIdx = statusColIdx + 1;

        // Append Status and Message headers to row 1
        Row headerRow = allRows[0];
        AppendInlineCell(headerRow, statusColIdx,  "Status");
        AppendInlineCell(headerRow, messageColIdx, "Message");

        // Build lookup: Excel row number → ProcessResult
        Dictionary<int, ProcessResult> resultMap = results
            .ToDictionary(r => r.RowNumber, r => r);

        // Write Status and Message for each data row
        foreach (Row row in allRows.Skip(1))
        {
            if (row.RowIndex is null)
                continue;

            int rowNum = (int)(uint)row.RowIndex;
            if (!resultMap.TryGetValue(rowNum, out ProcessResult? pr))
                continue;

            AppendInlineCell(row, statusColIdx,  pr.Status);
            AppendInlineCell(row, messageColIdx, pr.Message);
        }

        wsPart.Worksheet.Save();
    }

    // -------------------------------------------------------------------------
    // Private — OpenXML helpers
    // -------------------------------------------------------------------------

    private static SpreadsheetDocument OpenReadOnly(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Excel file not found: {filePath}", filePath);

        try
        {
            return SpreadsheetDocument.Open(filePath, isEditable: false);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Excel file could not be opened: {filePath}. Detail: {ex.Message}", ex);
        }
    }

    private static WorksheetPart GetFirstWorksheetPart(SpreadsheetDocument doc)
    {
        WorkbookPart workbookPart = doc.WorkbookPart
            ?? throw new InvalidOperationException("Workbook has no WorkbookPart.");

        WorksheetPart? wsPart = workbookPart.WorksheetParts.FirstOrDefault()
            ?? throw new InvalidOperationException("Workbook contains no worksheets.");

        return wsPart;
    }

    private static List<string?> BuildSharedStringTable(SpreadsheetDocument doc)
    {
        SharedStringTablePart? sstPart = doc.WorkbookPart?.SharedStringTablePart;
        if (sstPart is null)
            return new List<string?>();

        if (sstPart.SharedStringTable is null)
            return new List<string?>();

        return sstPart.SharedStringTable
            .Elements<SharedStringItem>()
            .Select(item => (string?)item.InnerText)
            .ToList();
    }

    /// <summary>
    /// Returns map of column index (0-based) → trimmed header name from the given header row.
    /// Column index is derived from each cell's CellReference attribute (e.g. "C1" → 2)
    /// so sparse rows with missing cells do not shift the mapping.
    /// </summary>
    private static Dictionary<int, string> BuildHeaderMap(Row headerRow, List<string?> sst)
    {
        var map = new Dictionary<int, string>();

        foreach (Cell cell in headerRow.Elements<Cell>())
        {
            int colIdx = CellRefToColIndex(cell.CellReference?.Value);
            string value = GetCellValue(cell, sst).Trim();
            if (!string.IsNullOrEmpty(value))
                map[colIdx] = value;
        }

        return map;
    }

    /// <summary>
    /// Returns map of column index (0-based) → trimmed cell value for the given row.
    /// Column index is derived from each cell's CellReference attribute so sparse rows
    /// with missing cells do not shift the mapping.
    /// </summary>
    private static Dictionary<int, string> ReadCells(Row row, List<string?> sst)
    {
        var map = new Dictionary<int, string>();

        foreach (Cell cell in row.Elements<Cell>())
        {
            int colIdx = CellRefToColIndex(cell.CellReference?.Value);
            map[colIdx] = GetCellValue(cell, sst).Trim();
        }

        return map;
    }

    private static bool IsBlankRow(Dictionary<int, string> cells)
        => cells.Values.All(v => string.IsNullOrWhiteSpace(v));

    private static string GetCellValue(Cell cell, List<string?> sst)
    {
        string? raw = cell.CellValue?.Text;
        if (raw is null)
            return string.Empty;

        if (cell.DataType?.Value == CellValues.SharedString
            && int.TryParse(raw, out int idx)
            && idx >= 0 && idx < sst.Count)
        {
            return sst[idx] ?? string.Empty;
        }

        return raw;
    }

    private static SchemaRow MapToSchemaRow(
        Dictionary<int, string> cells,
        Dictionary<int, string> headerMap)
    {
        // Build a header-name → value map (case-insensitive)
        var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<int, string> h in headerMap)
        {
            byName[h.Value] = cells.TryGetValue(h.Key, out string? v) ? v : string.Empty;
        }

        string GetCol(string name) =>
            byName.TryGetValue(name, out string? val) ? val : string.Empty;

        string isMandatoryRaw = GetCol(SchemaColumnNames.IsMandatory);

        return new SchemaRow
        {
            Collection  = GetCol(SchemaColumnNames.Collection),
            Property    = GetCol(SchemaColumnNames.Property),
            DataType    = GetCol(SchemaColumnNames.DataType),
            // IsMandatory: parsed strictly. Missing or non-TRUE/FALSE values left as
            // false here; SchemaService structural validation surfaces any real issues.
            IsMandatory = string.Equals(isMandatoryRaw, "TRUE", StringComparison.OrdinalIgnoreCase)
            || string.Equals(isMandatoryRaw, "YES", StringComparison.OrdinalIgnoreCase) 
            || string.Equals(isMandatoryRaw, "1", StringComparison.OrdinalIgnoreCase),
            Source      = GetCol(SchemaColumnNames.Source),
            Flow        = NullIfEmpty(GetCol(SchemaColumnNames.Flow)),
            FlowKey     = NullIfEmpty(GetCol(SchemaColumnNames.FlowKey)),
            JsonPath    = GetCol(SchemaColumnNames.JsonPath),
            Formula     = NullIfEmpty(GetCol(SchemaColumnNames.Formula)),
        };
    }

    private static string? NullIfEmpty(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>
    /// Appends an inline string cell to the row at the specified 0-based column index.
    /// Converts column index to Excel column letter(s) (A, B, … Z, AA, AB, …).
    /// </summary>
    private static void AppendInlineCell(Row row, int colIdx, string value)
    {
        string colLetter = ColIndexToLetter(colIdx);
        string cellRef   = $"{colLetter}{row.RowIndex}";

        var cell = new Cell
        {
            CellReference = cellRef,
            DataType      = CellValues.InlineString,
            InlineString  = new InlineString { Text = new Text(value) }
        };

        row.Append(cell);
    }

    private static string ColIndexToLetter(int index)
    {
        string result = string.Empty;
        int n = index;
        do
        {
            result = (char)('A' + n % 26) + result;
            n = n / 26 - 1;
        }
        while (n >= 0);
        return result;
    }

    /// <summary>
    /// Converts an OpenXML cell reference (e.g. "AB12") to a 0-based column index.
    /// Returns 0 for null/empty references rather than throwing.
    /// </summary>
    private static int CellRefToColIndex(string? cellRef)
    {
        if (string.IsNullOrEmpty(cellRef))
            return 0;

        int index = 0;
        foreach (char c in cellRef)
        {
            if (!char.IsLetter(c))
                break;
            index = index * 26 + (char.ToUpperInvariant(c) - 'A' + 1);
        }
        return index - 1; // convert to 0-based
    }
}
