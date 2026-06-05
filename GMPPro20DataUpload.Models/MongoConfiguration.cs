namespace GMPPro20DataUpload.Models;

/// <summary>
/// MongoDB connection settings. Populated from application configuration.
/// </summary>
public class MongoConfiguration
{
    /// <summary>Full MongoDB connection string.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Target database name.</summary>
    public string DatabaseName { get; set; } = string.Empty;
}
