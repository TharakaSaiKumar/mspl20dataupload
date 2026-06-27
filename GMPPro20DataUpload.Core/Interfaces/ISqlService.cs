namespace GMPPro20DataUpload.Core.Interfaces;

public interface ISqlService
{
    /// <summary>
    /// Executes the given SQL query against the database identified by connectionString,
    /// then returns the first row where the column named by lookupPath matches value
    /// (case-insensitive string comparison), serialised as a JSON object string.
    /// Returns null if no matching row is found.
    /// All column values are converted to string via ToString().
    /// </summary>
    Task<string?> QuerySingleAsync(string connectionString, string query, string lookupPath, string value);
}
