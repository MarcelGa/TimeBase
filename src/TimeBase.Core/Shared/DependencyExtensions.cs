using TimeBase.Core.Shared.ExceptionHandlers;
using TimeBase.Core.Shared.Services;

namespace TimeBase.Core.Shared;

public static class DependencyExtensions
{
    /// <summary>
    /// Adds shared cross-cutting services (metrics, exception handling, etc.).
    /// </summary>
    public static IServiceCollection AddShared(this IServiceCollection services)
    {
        // Register shared services
        services.AddSingleton<ITimeBaseMetrics, TimeBaseMetrics>();

        // Register global exception handler and ProblemDetails
        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();

        return services;
    }
}