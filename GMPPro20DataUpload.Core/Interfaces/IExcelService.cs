using GMPPro20DataUpload.Models;

namespace GMPPro20DataUpload.Core.Interfaces;

public interface IExcelService
{
    /// <summary>
    /// Reads all data rows from the uploaded Data Excel.
    /// All cell values are trimmed.
    /// Keys are column header names.
    /// </summary>
    List<Dictionary<string, string>> ReadDataRows(string filePath);

    /// <summary>
    /// Reads all schema rows from the Schema Excel, preserving row order.
    /// No sorting is applied.
    /// </summary>
    List<SchemaRow> ReadSchemaRows(string filePath);

    /// <summary>
    /// Creates the processed output Excel copy at destinationPath.
    /// Original file at sourcePath is not modified.
    /// Appends Status and Message columns populated from results.
    /// </summary>
    void WriteOutputFile(string sourcePath, string destinationPath, List<ProcessResult> results);
}
