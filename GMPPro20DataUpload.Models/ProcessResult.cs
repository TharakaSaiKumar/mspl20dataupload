namespace GMPPro20DataUpload.Models;

/// <summary>
/// Outcome of processing a single Excel data row across all collections.
/// Written to the output Excel as Status and Message columns.
/// </summary>
public class ProcessResult
{
    /// <summary>1-based Excel row number from the data file.</summary>
    public int RowNumber { get; set; }

    /// <summary>True when the row was processed without errors.</summary>
    public bool IsSuccess { get; set; }

    /// <summary>Short status label written to the output Excel (e.g. Success, Failed, Skipped).</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Detail message written to the output Excel.</summary>
    public string Message { get; set; } = string.Empty;
}
