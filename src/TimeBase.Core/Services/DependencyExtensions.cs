namespace TimeBase.Core.Services;

public static class DependencyExtensions
{
    public static IServiceCollection AddServices(this IServiceCollection services) 
        => services
            .AddSingleton<ITimeBaseMetrics, TimeBaseMetrics>()
            .AddScoped<ProviderRegistry>()
            .AddScoped<DataCoordinator>();
}