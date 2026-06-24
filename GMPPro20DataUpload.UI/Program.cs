using GMPPro20DataUpload.Core;
using GMPPro20DataUpload.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GMPPro20DataUpload.UI;

static class Program
{
    [STAThread]
    static void Main()
    {
        IConfiguration config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        MongoConfiguration mongoConfig = config.GetSection("MongoDB").Get<MongoConfiguration>()
            ?? new MongoConfiguration();

        ApplicationSettings appSettings = config.GetSection("Application").Get<ApplicationSettings>()
            ?? new ApplicationSettings();

        // Resolve TemplateDirectory to an absolute path anchored to the exe directory.
        if (!Path.IsPathRooted(appSettings.TemplateDirectory))
            appSettings.TemplateDirectory = Path.Combine(AppContext.BaseDirectory, appSettings.TemplateDirectory);

        // Resolve FormatsFile to an absolute path anchored to the exe directory.
        if (!Path.IsPathRooted(appSettings.FormatsFile))
            appSettings.FormatsFile = Path.Combine(AppContext.BaseDirectory, appSettings.FormatsFile);

        ServiceCollection services = new();

        services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Information);
        });

        services.AddSingleton(mongoConfig);
        services.AddSingleton(appSettings);
        services.AddCoreServices();
        services.AddTransient<Form1>();

        ServiceProvider provider = services.BuildServiceProvider();

        ApplicationConfiguration.Initialize();
        Application.Run(provider.GetRequiredService<Form1>());
    }
}