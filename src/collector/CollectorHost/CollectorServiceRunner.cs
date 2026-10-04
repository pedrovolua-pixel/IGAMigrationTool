using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CollectorHost;

public static class CollectorServiceRunner
{
    [SupportedOSPlatform("windows")]
    public static async Task RunAsync(string configPath)
    {
        using var host = Host.CreateDefaultBuilder()
            .UseWindowsService()
            .ConfigureServices(services => services.AddHostedService(provider =>
                new CollectorService(configPath,
                    provider.GetRequiredService<IHostApplicationLifetime>(),
                    provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<CollectorService>>())))
            .Build();
        await host.RunAsync();
    }
}
