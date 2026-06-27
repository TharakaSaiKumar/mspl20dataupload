using GMPPro20DataUpload.Core.Interfaces;
using Microsoft.Data.SqlClient;
using System.Text;
using System.Text.Json;

namespace GMPPro20DataUpload.Core.Services;

public class SqlService : ISqlService
{
    public async Task<string?> QuerySingleAsync(
        string connectionString,
        string query,
        string lookupPath,
        string value)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("SQL connection string must not be empty.");

        if (string.IsNullOrWhiteSpace(query))
            throw new InvalidOperationException("SQL query must not be empty.");

        using SqlConnection conn = new(connectionString);
        await conn.OpenAsync();

        using SqlCommand cmd = new(query, conn);
        using SqlDataReader reader = await cmd.ExecuteReaderAsync();

        // Find the ordinal of the lookup column (case-insensitive).
        int? lookupOrdinal = null;
        for (int i = 0; i < reader.FieldCount; i++)
        {
            if (string.Equals(reader.GetName(i), lookupPath, StringComparison.OrdinalIgnoreCase))
            {
                lookupOrdinal = i;
                break;
            }
        }

        if (lookupOrdinal is null)
            return null; // Column not found — no match possible.

        while (await reader.ReadAsync())
        {
            string columnValue = reader.IsDBNull(lookupOrdinal.Value)
                ? string.Empty
                : reader.GetValue(lookupOrdinal.Value)?.ToString() ?? string.Empty;

            if (!string.Equals(columnValue, value, StringComparison.OrdinalIgnoreCase))
                continue;

            // Serialise the matching row as a JSON object (column name → string value).
            var sb = new StringBuilder("{");
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (i > 0) sb.Append(',');
                string colName = reader.GetName(i);
                string colValue = reader.IsDBNull(i)
                    ? string.Empty
                    : reader.GetValue(i)?.ToString() ?? string.Empty;

                sb.Append(JsonSerializer.Serialize(colName));
                sb.Append(':');
                sb.Append(JsonSerializer.Serialize(colValue));
            }
            sb.Append('}');
            return sb.ToString();
        }

        return null;
    }
}
