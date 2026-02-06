using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using TimeBase.Core.Shared.Services;

namespace TimeBase.Core.Providers.Services;

/// <summary>
/// Background service that periodically checks provider health and updates their status.
/// </summary>
public class ProviderHealthMonitor(
    ILogger<ProviderHealthMonitor> logger,
    IServiceProvider serviceProvider,
    ITimeBaseMetrics metrics) : BackgroundService
{
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Provider health monitor started");

        // Wait a bit before first check to allow providers to start up
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAllProvidersHealthAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during provider health check");
            }

            // Wait for next check interval
            await Task.Delay(_checkInterval, stoppingToken);
        }

        logger.LogInformation("Provider health monitor stopped");
    }

    private async Task CheckAllProvidersHealthAsync(CancellationToken cancellationToken)
    {
        // Create a scope to get scoped services (DbContext is scoped)
        using var scope = serviceProvider.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<IProviderRegistry>();
        var providerClient = scope.ServiceProvider.GetRequiredService<IProviderClient>();

        logger.LogDebug("Checking health of all enabled providers");

        var providers = await registry.GetAllProvidersAsync(enabled: true);

        foreach (var provider in providers)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                var isHealthy = await providerClient.IsHealthyAsync(provider);

                if (isHealthy)
                {
                    logger.LogDebug("Provider {Slug} is healthy", provider.Slug);
                    metrics.RecordProviderHealth(provider.Slug, healthy: true);
                }
                else
                {
                    logger.LogWarning("Provider {Slug} is unhealthy or unreachable", provider.Slug);
                    metrics.RecordProviderHealth(provider.Slug, healthy: false);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to check health of provider {Slug}", provider.Slug);
                metrics.RecordProviderHealth(provider.Slug, healthy: false);
            }
        }

        logger.LogDebug("Completed health check for {Count} providers", providers.Count);
    }
}