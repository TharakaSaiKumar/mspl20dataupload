namespace GMPPro20DataUpload.Models;

/// <summary>
/// Outcome of a pre-processing validation step (MongoDB connection, schema file, JSON templates, Excel file).
/// Processing must stop if IsValid is false.
/// </summary>
public class ValidationResult
{
    /// <summary>True when all validation checks passed.</summary>
    public bool IsValid { get; set; }

    /// <summary>One entry per validation failure. Empty when IsValid is true.</summary>
    public List<string> Errors { get; set; } = new();
}
