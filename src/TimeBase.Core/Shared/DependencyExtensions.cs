using TimeBase.Core.Shared.Services;

namespace TimeBase.Core.Shared;

public static class DependencyExtensions
{
    /// <summary>
    /// Adds shared cross-cutting services (metrics, etc.).
    /// </summary>
    public static IServiceCollection AddShared(this IServiceCollection services)
    {
        // Register shared services
        services.AddSingleton<ITimeBaseMetrics, TimeBaseMetrics>();

        return services;
    }
}