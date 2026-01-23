namespace TimeBase.Core.Services;

public static class DependencyExtensions
{
    public static IServiceCollection AddServices(this IServiceCollection services) 
        => services
            .AddSingleton<TimeBaseMetrics>()
            .AddScoped<ProviderRegistry>()
            .AddScoped<DataCoordinator>();
}