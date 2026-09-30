using System.Runtime.Versioning;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CollectorHost;

[SupportedOSPlatform("windows")]
public sealed class CollectorService(
    string configPath,
    IHostApplicationLifetime lifetime,
    ILogger<CollectorService> logger) : BackgroundService
{
    private readonly CollectorRunCoordinator coordinator = new(new PendingCollectorRunAdapter());

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        DateTimeOffset? next = null;
        string? scheduleKey = null;
        string? lastState = null;

        while (!stoppingToken.IsCancellationRequested)
        {
            CollectorConfig config;
            try
            {
                config = WindowsProtectedConfig.Read(configPath);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or
                   InvalidOperationException or FormatException or System.Security.SecurityException)
            {
                logger.LogError("CONFIG_INVALID");
                lifetime.StopApplication();
                return;
            }

            var state = config.Enabled ? "SOURCE_CONTRACT_PENDING" : "COLLECTOR_DISABLED";
            if (state != lastState)
            {
                logger.LogInformation("{CollectorState}", state);
                lastState = state;
            }

            var delay = TimeSpan.FromHours(1);
            if (config.Enabled)
            {
                var key = $"{config.TimeZoneId}|{config.LocalRunTime:HH:mm}";
                var now = DateTimeOffset.UtcNow;
                if (key != scheduleKey || next is null)
                {
                    next = DailySchedule.Next(now,
                        TimeZoneInfo.FindSystemTimeZoneById(config.TimeZoneId), config.LocalRunTime);
                    scheduleKey = key;
                }

                if (now >= next)
                {
                    var result = await coordinator.RunAsync(configPath, config, stoppingToken);
                    logger.LogWarning("{CollectorRunState}", result.Outcome == CollectorRunOutcome.SourceContractPending
                        ? "RUN_NOT_STARTED_CONTRACT_PENDING" : "RUN_NOT_STARTED");
                    next = DailySchedule.Next(now,
                        TimeZoneInfo.FindSystemTimeZoneById(config.TimeZoneId), config.LocalRunTime);
                }

                delay = next.Value - now < delay ? next.Value - now : delay;
                if (delay < TimeSpan.FromSeconds(1))
                {
                    delay = TimeSpan.FromSeconds(1);
                }
            }
            else
            {
                next = null;
                scheduleKey = null;
            }

            await Task.Delay(delay, stoppingToken);
        }
    }
}
