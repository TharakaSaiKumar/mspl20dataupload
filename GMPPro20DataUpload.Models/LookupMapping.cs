namespace GMPPro20DataUpload.Models;

/// <summary>
/// Describes a single lookup entry from Application:LookupMappings in appsettings.json.
/// </summary>
public class LookupMapping
{
    /// <summary>How the lookup input is determined. Values: "static" | "excel".</summary>
    public string InputType { get; set; } = string.Empty;

    /// <summary>For InputType=static: the fixed value to query (e.g. "ACT").</summary>
    public string? InputValue { get; set; }

    /// <summary>For InputType=excel: the Excel column name to read the query value from.</summary>
    public string? InputColumn { get; set; }

    /// <summary>The MongoDB collection to search.</summary>
    public string Collection { get; set; } = string.Empty;

    /// <summary>Dot-notation field path to match against (e.g. "statusCode").</summary>
    public string LookupPath { get; set; } = string.Empty;

    /// <summary>How the output is written. Values: "objectid" | "object".</summary>
    public string OutputType { get; set; } = string.Empty;

    /// <summary>For OutputType=objectid: the field path in the found document to extract (e.g. "_id").</summary>
    public string? OutputPath { get; set; }

    /// <summary>
    /// For OutputType=object: maps target property names to source paths in the found document.
    /// A null value writes JSON null; a non-null value navigates the found document.
    /// </summary>
    public Dictionary<string, string?>? Mappings { get; set; }
}
