using GMPPro20DataUpload.Core.Interfaces;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GMPPro20DataUpload.Core.Services;

public class TemplateService : ITemplateService
{

    private readonly ICacheService _cacheService;

    public TemplateService(ICacheService cacheService)
    {
        _cacheService = cacheService;
    }

    public bool TemplateExists(string templateDirectory, string collectionName)
        => File.Exists(ResolvePath(templateDirectory, collectionName));

    public string LoadTemplate(string templateDirectory, string collectionName)
    {
        string path = ResolvePath(templateDirectory, collectionName);
        string cacheKey = BuildCacheKey(path);

        if (_cacheService.TryGet(cacheKey, out string? cachedJson) &&
            !string.IsNullOrWhiteSpace(cachedJson))
        {
            return cachedJson;
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Template file not found for collection '{collectionName}'.", path);
        }

        string json = File.ReadAllText(path);
        _cacheService.Add(cacheKey, json);

        return json;
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

    private static string BuildCacheKey(string path)
            => $"template:{Path.GetFullPath(path)}";
}
