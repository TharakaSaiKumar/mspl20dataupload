namespace GMPPro20DataUpload.Core.Constants;

/// <summary>
/// Column header names as they appear in the Schema Excel file.
/// Matching against actual headers must be case-insensitive.
/// </summary>
public static class SchemaColumnNames
{
    public const string Collection  = "Collection";
    public const string Property    = "Property";
    public const string DataType    = "DataType";
    public const string IsMandatory = "IsMandatory";
    public const string Source      = "source";
    public const string Flow        = "flow";
    public const string FlowKey     = "flowkey";
    public const string JsonPath    = "jsonpath";
}
