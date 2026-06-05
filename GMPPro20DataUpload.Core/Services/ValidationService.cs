using GMPPro20DataUpload.Core.Interfaces;
using GMPPro20DataUpload.Models;

namespace GMPPro20DataUpload.Core.Services;

public class ValidationService : IValidationService
{
    private readonly IMongoService _mongoService;
    private readonly ISchemaService _schemaService;
    private readonly ITemplateService _templateService;
    private readonly IExcelService _excelService;

    public ValidationService(
        IMongoService mongoService,
        ISchemaService schemaService,
        ITemplateService templateService,
        IExcelService excelService)
    {
        _mongoService = mongoService;
        _schemaService = schemaService;
        _templateService = templateService;
        _excelService = excelService;
    }

    public Task<ValidationResult> ValidateAsync(
        string schemaFilePath,
        string dataFilePath,
        string templateDirectory,
        MongoConfiguration mongoConfig)
        => throw new NotImplementedException();
}
