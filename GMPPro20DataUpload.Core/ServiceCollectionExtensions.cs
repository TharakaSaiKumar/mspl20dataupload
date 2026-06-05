using GMPPro20DataUpload.Core.Interfaces;
using GMPPro20DataUpload.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GMPPro20DataUpload.Core;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all Core services with the DI container.
    /// Call this from Program.cs during startup.
    /// </summary>
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        services.AddSingleton<IExcelService, ExcelService>();
        services.AddSingleton<ISchemaService, SchemaService>();
        services.AddSingleton<ITemplateService, TemplateService>();
        services.AddSingleton<IMongoService, MongoService>();
        services.AddSingleton<IFlowService, FlowService>();
        services.AddSingleton<ICacheService, CacheService>();

        services.AddTransient<IValidationService, ValidationService>();
        services.AddTransient<IProcessingService, ProcessingService>();

        return services;
    }
}
