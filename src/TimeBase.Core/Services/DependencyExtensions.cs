namespace TimeBase.Core.Services;

public static class DependencyExtensions
{
    public static IServiceCollection AddServices(this IServiceCollection services) 
        => services
            .AddSingleton<ITimeBaseMetrics, TimeBaseMetrics>()
            .AddSingleton<IProviderClient, ProviderClient>()  // Singleton for channel pooling
            .AddScoped<ProviderRegistry>()
            .AddScoped<DataCoordinator>()
            .AddHostedService<ProviderHealthMonitor>();  // Background service for health monitoring
}