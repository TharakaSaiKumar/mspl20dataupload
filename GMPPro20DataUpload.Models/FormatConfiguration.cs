namespace GMPPro20DataUpload.Models;

/// <summary>
/// Represents a single format entry from the formats configuration file.
/// Each format defines the resources and settings required to process a specific Excel format.
/// </summary>
public class FormatConfiguration
{
    /// <summary>Unique identifier for this format (e.g. USERS).</summary>
    public string FormatKey { get; set; } = string.Empty;

    /// <summary>Display name shown in the format selection dropdown.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Module code used during processing (e.g. USERS, DESIGNATIONS).</summary>
    public string ModuleCode { get; set; } = string.Empty;

    /// <summary>
    /// Schema Excel filename. Resolved relative to ApplicationSettings.TemplateDirectory at runtime.
    /// </summary>
    public string SchemaFile { get; set; } = string.Empty;

    /// <summary>
    /// Template JSON filename. Resolved relative to ApplicationSettings.TemplateDirectory at runtime.
    /// Validated to exist before processing begins.
    /// </summary>
    public string TemplateFile { get; set; } = string.Empty;
}
