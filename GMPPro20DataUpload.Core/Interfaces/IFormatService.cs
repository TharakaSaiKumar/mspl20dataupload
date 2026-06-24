using GMPPro20DataUpload.Models;

namespace GMPPro20DataUpload.Core.Interfaces;

public interface IFormatService
{
    /// <summary>
    /// Loads all format configurations from the given JSON file.
    /// Each entry is validated for required fields (FormatKey, DisplayName, ModuleCode,
    /// SchemaFile, TemplateFile must all be non-empty).
    /// Throws FileNotFoundException if the file does not exist.
    /// Throws InvalidOperationException if the file is invalid JSON, empty, or contains
    /// validation errors.
    /// </summary>
    List<FormatConfiguration> LoadFormats(string formatsFilePath);
}
