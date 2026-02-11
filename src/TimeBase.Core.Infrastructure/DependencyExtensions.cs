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
        // Skip migrations in Docker - init.sql handles schema creation
        // EF migrations conflict with TimescaleDB features (hypertables, continuous aggregates)
        var skipMigrations = Environment.GetEnvironmentVariable("SKIP_EF_MIGRATIONS");
        if (!string.IsNullOrEmpty(skipMigrations) && skipMigrations.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            using var scope = serviceProvider.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<TimeBaseDbContext>>();
            logger.LogInformation("Skipping EF migrations (SKIP_EF_MIGRATIONS=true), using init.sql schema");
            return;
        }

        // Apply EF migrations with proper logging
        using var migrationScope = serviceProvider.CreateScope();
        var db = migrationScope.ServiceProvider.GetRequiredService<TimeBaseDbContext>();
        var migrationLogger = migrationScope.ServiceProvider.GetRequiredService<ILogger<TimeBaseDbContext>>();

        try
        {
            migrationLogger.LogInformation("Applying database migrations...");
            db.Database.Migrate();
            migrationLogger.LogInformation("Database migrations applied successfully");
        }
        catch (Exception ex)
        {
            migrationLogger.LogError(ex, "Failed to apply database migrations");
            throw;
        }
    }
}