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

        ServiceCollection services = new();

        services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Information);
        });

        services.AddSingleton(mongoConfig);
        services.AddCoreServices();
        services.AddTransient<Form1>();

        ServiceProvider provider = services.BuildServiceProvider();

        ApplicationConfiguration.Initialize();
        Application.Run(provider.GetRequiredService<Form1>());
    }
}