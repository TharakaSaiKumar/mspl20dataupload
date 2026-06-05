using System.Text.Json.Nodes;

namespace GMPPro20DataUpload.Core.Interfaces;

public interface ITemplateService
{
    /// <summary>
    /// Loads the JSON template file for the given collection name and returns its raw content.
    /// Template files are application-deployed and not user-uploaded.
    /// Convention: {templateDirectory}/{collectionName}.json
    /// Throws FileNotFoundException if the file does not exist.
    /// Throws InvalidOperationException if the file is not valid JSON.
    /// </summary>
    string LoadTemplate(string templateDirectory, string collectionName);

    /// <summary>
    /// Returns true if the JSON template file exists for the given collection.
    /// </summary>
    bool TemplateExists(string templateDirectory, string collectionName);

    /// <summary>
    /// Loads the JSON template for the given collection and returns it as a mutable JsonNode.
    /// A fresh node tree is parsed on every call — callers receive an independent copy.
    /// Throws FileNotFoundException if the file does not exist.
    /// Throws InvalidOperationException if the file is not valid JSON.
    /// </summary>
    JsonNode LoadTemplateAsNode(string templateDirectory, string collectionName);
}
