namespace TimeBase.Core.Services;

public static class DependencyExtensions
{
    public static IServiceCollection AddServices(this IServiceCollection services)
        => services
            .AddSingleton<ITimeBaseMetrics, TimeBaseMetrics>()
            .AddSingleton<IProviderClient, ProviderClient>()  // Singleton for channel pooling
            .AddSingleton<IMarketBroadcaster, MarketBroadcaster>()  // Singleton for SignalR broadcasting
            .AddSingleton<RealTimeStreamingService>()  // Singleton for managing subscriptions
            .AddHostedService(sp => sp.GetRequiredService<RealTimeStreamingService>())  // Register as hosted service
            .AddScoped<IProviderRegistry, ProviderRegistry>()
            .AddScoped<IDataCoordinator, DataCoordinator>()
            .AddHostedService<ProviderHealthMonitor>();  // Background service for health monitoring
}