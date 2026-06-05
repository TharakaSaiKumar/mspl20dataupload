using GMPPro20DataUpload.Core.Interfaces;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GMPPro20DataUpload.Core.Services;

public class TemplateService : ITemplateService
{
    public bool TemplateExists(string templateDirectory, string collectionName)
        => File.Exists(ResolvePath(templateDirectory, collectionName));

    public string LoadTemplate(string templateDirectory, string collectionName)
    {
        string path = ResolvePath(templateDirectory, collectionName);

        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"Template file not found for collection '{collectionName}'.", path);

        return File.ReadAllText(path);
    }

    public JsonNode LoadTemplateAsNode(string templateDirectory, string collectionName)
    {
        string json = LoadTemplate(templateDirectory, collectionName);

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Template file for collection '{collectionName}' contains invalid JSON. {ex.Message}", ex);
        }

        if (node is null)
            throw new InvalidOperationException(
                $"Template file for collection '{collectionName}' parsed as null.");

        return node;
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static string ResolvePath(string templateDirectory, string collectionName)
        => Path.Combine(templateDirectory, $"{collectionName}.json");
}
