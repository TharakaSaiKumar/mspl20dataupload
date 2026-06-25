namespace GMPPro20DataUpload.Models;

/// <summary>
/// Represents a single row from the Schema Excel file.
/// Column order: Collection, Property, DataType, IsMandatory, Source, Flow, FlowKey, JsonPath
/// </summary>
public class SchemaRow
{
    /// <summary>MongoDB collection name (e.g. masterDesignations).</summary>
    public string Collection { get; set; } = string.Empty;

    /// <summary>Field/property name within the document.</summary>
    public string Property { get; set; } = string.Empty;

    /// <summary>Data type: text | integer | datetime | objectid</summary>
    public string DataType { get; set; } = string.Empty;

    /// <summary>Whether the field is mandatory.</summary>
    public bool IsMandatory { get; set; }

    /// <summary>Value origin: excel | compute | auto</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Flow action: publish | consume | empty</summary>
    public string? Flow { get; set; }

    /// <summary>Unique key used to publish or consume a value in FlowContext.</summary>
    public string? FlowKey { get; set; }

    /// <summary>Full dot-notation JsonPath for placing the value in the document (e.g. systemData.createdOn).</summary>
    public string JsonPath { get; set; } = string.Empty;

    /// <summary>Formula expression for Source=formula rows (e.g. "{propertyA} + {propertyB}"). Null for all other source types.</summary>
    public string? Formula { get; set; }
}
