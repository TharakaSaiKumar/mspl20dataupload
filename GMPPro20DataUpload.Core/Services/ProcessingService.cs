using GMPPro20DataUpload.Core.Interfaces;
using GMPPro20DataUpload.Models;

namespace GMPPro20DataUpload.Core.Services;

public class ProcessingService : IProcessingService
{
    private readonly ISchemaService _schemaService;
    private readonly ITemplateService _templateService;
    private readonly IExcelService _excelService;
    private readonly IMongoService _mongoService;
    private readonly IFlowService _flowService;
    private readonly ICacheService _cacheService;

    public ProcessingService(
        ISchemaService schemaService,
        ITemplateService templateService,
        IExcelService excelService,
        IMongoService mongoService,
        IFlowService flowService,
        ICacheService cacheService)
    {
        _schemaService = schemaService;
        _templateService = templateService;
        _excelService = excelService;
        _mongoService = mongoService;
        _flowService = flowService;
        _cacheService = cacheService;
    }

    public Task<ProcessingContext> ProcessAsync(
        string schemaFilePath,
        string dataFilePath,
        string templateDirectory,
        MongoConfiguration mongoConfig,
        IProgress<string> progress,
        CancellationToken cancellationToken)
        => throw new NotImplementedException();
}
