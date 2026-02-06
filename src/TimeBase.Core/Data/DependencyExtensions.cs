using Microsoft.AspNetCore.Builder;

using TimeBase.Core.Data.Hubs;
using TimeBase.Core.Data.Services;

namespace TimeBase.Core.Data;

public static class DependencyExtensions
{
    /// <summary>
    /// Adds the Data feature module (services, SignalR hubs, endpoints, background jobs).
    /// </summary>
    public static IServiceCollection AddData(this IServiceCollection services)
    {
        // Register data services
        services.AddScoped<IDataCoordinator, DataCoordinator>();
        services.AddSingleton<IMarketBroadcaster, MarketBroadcaster>();  // Singleton for SignalR broadcasting

        // Register real-time streaming service (both as singleton and hosted service)
        services.AddSingleton<RealTimeStreamingService>();
        services.AddHostedService(sp => sp.GetRequiredService<RealTimeStreamingService>());

        return services;
    }

    /// <summary>
    /// Maps data query endpoints and SignalR hubs.
    /// </summary>
    public static IEndpointRouteBuilder MapData(this IEndpointRouteBuilder app, IEndpointRouteBuilder? apiGroup = null)
    {
        // Map API endpoints
        app.AddDataEndpoints(apiGroup);

        // Map SignalR hub
        app.MapHub<MarketHub>("/hubs/market");

        return app;
    }
}