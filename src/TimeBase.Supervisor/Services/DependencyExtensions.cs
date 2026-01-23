namespace TimeBase.Supervisor.Services;

public static class DependencyExtensions
{
    public static IServiceCollection AddServices(this IServiceCollection services) 
        => services
            .AddSingleton<ProviderRegistry>()
            .AddSingleton<DataCoordinator>();
}