using GMPPro20DataUpload.Core.Interfaces;
using GMPPro20DataUpload.Models;
using System.Text.Json;

namespace GMPPro20DataUpload.Core.Services;

public class FormatService : IFormatService
{
    public List<FormatConfiguration> LoadFormats(string formatsFilePath)
    {
        if (!File.Exists(formatsFilePath))
            throw new FileNotFoundException(
                $"Formats configuration file not found: {formatsFilePath}", formatsFilePath);

        string json;
        try
        {
            json = File.ReadAllText(formatsFilePath);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Formats configuration file could not be read: {formatsFilePath}. Detail: {ex.Message}", ex);
        }

        List<FormatConfiguration>? formats;
        try
        {
            formats = JsonSerializer.Deserialize<List<FormatConfiguration>>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Formats configuration file contains invalid JSON. {ex.Message}", ex);
        }

        if (formats is null || formats.Count == 0)
            throw new InvalidOperationException(
                "Formats configuration file contains no format entries.");

        var errors = new List<string>();

        for (int i = 0; i < formats.Count; i++)
        {
            FormatConfiguration fmt = formats[i];
            string loc = $"Format[{i}] (FormatKey='{fmt.FormatKey}')";

            if (string.IsNullOrWhiteSpace(fmt.FormatKey))
                errors.Add($"{loc}: FormatKey must not be empty.");

            if (string.IsNullOrWhiteSpace(fmt.DisplayName))
                errors.Add($"{loc}: DisplayName must not be empty.");

            if (string.IsNullOrWhiteSpace(fmt.ModuleCode))
                errors.Add($"{loc}: ModuleCode must not be empty.");

            if (string.IsNullOrWhiteSpace(fmt.RequestPrefix))
                errors.Add($"{loc}: RequestPrefix must not be empty.");

            if (string.IsNullOrWhiteSpace(fmt.SchemaFile))
                errors.Add($"{loc}: SchemaFile must not be empty.");

            if (string.IsNullOrWhiteSpace(fmt.TemplateFile))
                errors.Add($"{loc}: TemplateFile must not be empty.");
        }

        if (errors.Count > 0)
            throw new InvalidOperationException(
                $"Formats configuration validation failed:\n{string.Join("\n", errors)}");

        return formats;
    }
}
