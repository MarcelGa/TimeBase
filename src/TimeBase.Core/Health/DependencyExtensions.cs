namespace TimeBase.Core.Health;

public static class DependencyExtensions
{
    public static IServiceCollection AddHealthChecks(this IServiceCollection services, out IHealthChecksBuilder healthChecksBuilder)
    {
        healthChecksBuilder = services.AddHealthChecks();

        return services;
    }
}