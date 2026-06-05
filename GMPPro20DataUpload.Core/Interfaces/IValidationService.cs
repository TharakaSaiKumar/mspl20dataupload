using GMPPro20DataUpload.Models;

namespace GMPPro20DataUpload.Core.Interfaces;

public interface IValidationService
{
    /// <summary>
    /// Runs all pre-processing validation checks in order:
    /// 1. MongoDB connection reachable
    /// 2. Schema file present and parseable
    /// 3. All JSON templates present for collections in schema
    /// 4. Data Excel present and readable
    ///
    /// Returns a ValidationResult with IsValid = false and populated Errors
    /// if any check fails. Processing must not start if IsValid is false.
    /// </summary>
    Task<ValidationResult> ValidateAsync(
        string schemaFilePath,
        string dataFilePath,
        string templateDirectory,
        MongoConfiguration mongoConfig);
}
