namespace GMPPro20DataUpload.Models;

/// <summary>
/// Application-level settings bound from appsettings.json Application section.
/// </summary>
public class ApplicationSettings
{
    /// <summary>Suffix appended to formattedReferenceNumber to form referenceNumber (e.g. /00).</summary>
    public string ReferenceSuffix { get; set; } = string.Empty;

    /// <summary>Root directory containing JSON template files. Resolved to an absolute path at startup.</summary>
    public string TemplateDirectory { get; set; } = "Templates";

    /// <summary>
    /// Path to the formats configuration JSON file. Resolved to an absolute path at startup.
    /// </summary>
    public string FormatsFile { get; set; } = "formats.json";

    /// <summary>Maps module codes to reference prefixes (e.g. USERS → USR).</summary>
    public Dictionary<string, string> ModuleMappings { get; set; } = new();

    /// <summary>Maps lookup keys to their lookup definitions (e.g. activeStatus, primaryUnit).</summary>
    public Dictionary<string, LookupMapping> LookupMappings { get; set; } = new();

    /// <summary>
    /// Returns the module prefix for the given module code.
    /// Throws InvalidOperationException if no mapping is found.
    /// </summary>
    public string GetModulePrefix(string moduleCode)
    {
        if (ModuleMappings.TryGetValue(moduleCode, out string? prefix))
            return prefix;

        throw new InvalidOperationException(
            $"No module mapping found for moduleCode '{moduleCode}'. " +
            "Check Application:ModuleMappings in appsettings.json.");
    }
}
