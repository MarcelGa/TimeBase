using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TimeBase.Core.Infrastructure.Data;

namespace TimeBase.Core.Tests.Integration.Infrastructure;

public class TimeBaseWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainerFixture _dbFixture;

    public TimeBaseWebApplicationFactory()
    {
        _dbFixture = new PostgreSqlContainerFixture();
    }

    public string ConnectionString => _dbFixture.ConnectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Set testing environment to skip Serilog configuration
        builder.UseEnvironment("Testing");
        
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Override configuration for testing
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:TimeBaseDb"] = _dbFixture.ConnectionString,
                ["OpenTelemetry:Tracing:Enabled"] = "false",
                ["OpenTelemetry:Metrics:Enabled"] = "false",
                ["OpenTelemetry:Tracing:ConsoleExporter"] = "false",
                ["OpenTelemetry:Tracing:OtlpExporter"] = "false",
                ["OpenTelemetry:Metrics:ConsoleExporter"] = "false",
                ["OpenTelemetry:Metrics:OtlpExporter"] = "false",
                ["IpRateLimiting:GeneralRules:0:Limit"] = "10000", // Disable rate limiting for tests
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Remove the existing DbContext configuration
            services.RemoveAll<DbContextOptions<TimeBaseDbContext>>();
            services.RemoveAll<TimeBaseDbContext>();

            // Add DbContext with test database connection
            services.AddDbContext<TimeBaseDbContext>(options =>
            {
                options.UseNpgsql(_dbFixture.ConnectionString);
            });

            // Note: Health checks will automatically use the updated connection string
            // since they depend on the DbContext and the configuration we override above

            // Build the service provider to run migrations
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TimeBaseDbContext>();
            db.Database.Migrate();
        });
    }

    public async Task ResetDatabaseAsync()
    {
        await _dbFixture.ResetDatabaseAsync();
    }

    async Task IAsyncLifetime.InitializeAsync()
    {
        await _dbFixture.InitializeAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _dbFixture.DisposeAsync();
    }
}
