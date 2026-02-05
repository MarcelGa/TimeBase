using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using TimeBase.Core.Infrastructure.Data;

namespace TimeBase.Core.Infrastructure;

public static class DependencyExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, IHealthChecksBuilder healthChecksBuilder)
    {
        services.AddDbContext<TimeBaseDbContext>(opts =>
            opts.UseNpgsql(configuration.GetConnectionString("TimeBaseDb")));

        // Add health checks
        // Note: Only using DbContext health check to ensure it works properly in tests
        // The DbContext check will verify PostgreSQL/TimescaleDB connectivity
        healthChecksBuilder
            .AddDbContextCheck<TimeBaseDbContext>(name: "database", tags: new[] { "db", "ready" });

        return services;
    }

    public static void UseInfrastructure(this IServiceProvider serviceProvider)
    {
        // Apply EF migrations with proper logging
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TimeBaseDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<TimeBaseDbContext>>();

        try
        {
            logger.LogInformation("Applying database migrations...");
            db.Database.Migrate();
            logger.LogInformation("Database migrations applied successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to apply database migrations");
        }
    }
}