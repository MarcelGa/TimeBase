namespace TimeBase.Core.Services;

public static class DependencyExtensions
{
    public static IServiceCollection AddServices(this IServiceCollection services) 
        => services
            .AddScoped<ProviderRegistry>()
            .AddScoped<DataCoordinator>();
}