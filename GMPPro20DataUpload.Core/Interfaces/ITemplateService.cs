namespace GMPPro20DataUpload.Core.Interfaces;

public interface ITemplateService
{
    /// <summary>
    /// Loads the JSON template file for the given collection name.
    /// Template files are application-deployed and not user-uploaded.
    /// Convention: {templateDirectory}/{collectionName}.json
    /// </summary>
    string LoadTemplate(string templateDirectory, string collectionName);

    /// <summary>
    /// Returns true if the JSON template file exists for the given collection.
    /// </summary>
    bool TemplateExists(string templateDirectory, string collectionName);
}
