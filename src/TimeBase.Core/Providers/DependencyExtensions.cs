using TimeBase.Core.Providers.Services;

namespace TimeBase.Core.Providers;

public static class DependencyExtensions
{
    /// <summary>
    /// Adds the Providers feature module (services, endpoints, background jobs).
    /// </summary>
    public static IServiceCollection AddProviders(this IServiceCollection services)
    {
        // Register provider services
        services.AddSingleton<IProviderClient, ProviderClient>();  // Singleton for gRPC channel pooling
        services.AddScoped<IProviderRegistry, ProviderRegistry>();

        // Register background services
        services.AddHostedService<ProviderHealthMonitor>();

        return services;
    }

    /// <summary>
    /// Maps provider management endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapProviders(this IEndpointRouteBuilder app, IEndpointRouteBuilder? apiGroup = null)
    {
        app.AddProviderEndpoints(apiGroup);
        return app;
    }
}