namespace GMPPro20DataUpload.Models;

/// <summary>
/// Holds all state for a single processing run.
/// Passed through Core services; reported back to the UI layer.
/// </summary>
public class ProcessingContext
{
    /// <summary>Filename of the uploaded data Excel (stored as traceability metadata on each document).</summary>
    public string ExcelFilename { get; set; } = string.Empty;

    /// <summary>Module code used to build the request code (e.g. DE, ORG).</summary>
    public string ModuleCode { get; set; } = string.Empty;

    /// <summary>Generated request code for this run (e.g. DE01). Set during processing.</summary>
    public string? RequestCode { get; set; }

    /// <summary>Total number of data rows in the uploaded Excel.</summary>
    public int TotalRows { get; set; }

    /// <summary>Number of rows processed so far (for progress reporting).</summary>
    public int ProcessedRows { get; set; }

    /// <summary>True when the user has requested an abort. Processing finishes the current row then stops.</summary>
    public bool IsAborted { get; set; }

    /// <summary>Cross-collection publish/consume store for this run.</summary>
    public FlowContext FlowContext { get; set; } = new();

    /// <summary>Per-row outcomes. Written to the output Excel as Status and Message columns.</summary>
    public List<ProcessResult> Results { get; set; } = new();
}
