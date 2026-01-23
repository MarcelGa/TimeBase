using Microsoft.EntityFrameworkCore;
using TimeBase.Core.Infrastructure.Data;
using TimeBase.Core.Services;
using TimeBase.Core;
using Serilog;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using FluentValidation;
using AspNetCoreRateLimit;

// Configure Serilog early
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting TimeBase.Core");

    var builder = WebApplication.CreateBuilder(args);

    // Replace default logging with Serilog
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // Add OpenTelemetry
    var otelConfig = builder.Configuration.GetSection("OpenTelemetry");
    var serviceName = otelConfig["ServiceName"] ?? "TimeBase.Core";
    var serviceVersion = otelConfig["ServiceVersion"] ?? "1.0.0";

    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource
            .AddService(serviceName: serviceName, serviceVersion: serviceVersion))
        .WithTracing(tracing =>
        {
            tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation();

            if (otelConfig.GetValue<bool>("Tracing:ConsoleExporter"))
                tracing.AddConsoleExporter();

            if (otelConfig.GetValue<bool>("Tracing:OtlpExporter"))
                tracing.AddOtlpExporter(opts =>
                {
                    opts.Endpoint = new Uri(otelConfig["Tracing:OtlpEndpoint"] ?? "http://localhost:4317");
                });
        })
        .WithMetrics(metrics =>
        {
            metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter("TimeBase.Core"); // Add custom TimeBase metrics

            if (otelConfig.GetValue<bool>("Metrics:ConsoleExporter"))
                metrics.AddConsoleExporter();

            if (otelConfig.GetValue<bool>("Metrics:OtlpExporter"))
                metrics.AddOtlpExporter(opts =>
                {
                    opts.Endpoint = new Uri(otelConfig["Metrics:OtlpEndpoint"] ?? "http://localhost:4317");
                });

            // Always add Prometheus exporter for scraping
            metrics.AddPrometheusExporter();
        });

    builder.Services.AddDbContext<TimeBaseDbContext>(opts =>
        opts.UseNpgsql(builder.Configuration.GetConnectionString("TimeBaseDb")));
    
    // Add health checks
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<TimeBaseDbContext>(name: "database", tags: new[] { "db", "ready" })
        .AddNpgSql(
            builder.Configuration.GetConnectionString("TimeBaseDb") ?? throw new InvalidOperationException("Database connection string not configured"),
            name: "timescaledb",
            tags: new[] { "db", "ready" });
    
    // Add FluentValidation
    builder.Services.AddValidatorsFromAssemblyContaining<Program>();
    
    // Add rate limiting
    builder.Services.AddMemoryCache();
    builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
    builder.Services.AddInMemoryRateLimiting();
    builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
    
    builder.Services.AddServices();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    var app = builder.Build();

    // Add Serilog request logging
    app.UseSerilogRequestLogging();

    // Add rate limiting
    app.UseIpRateLimiting();

    // Add Prometheus scraping endpoint
    app.MapPrometheusScrapingEndpoint();

    // Add health check endpoints
    app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => false, // No checks, just returns that the app is running
        AllowCachingResponses = false
    });
    
    app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"), // Only checks tagged with "ready"
        AllowCachingResponses = false,
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            var result = System.Text.Json.JsonSerializer.Serialize(new
            {
                status = report.Status.ToString(),
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    description = e.Value.Description,
                    duration = e.Value.Duration.TotalMilliseconds
                }),
                totalDuration = report.TotalDuration.TotalMilliseconds
            });
            await context.Response.WriteAsync(result);
        }
    });
    
    app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => true, // All health checks
        AllowCachingResponses = false,
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            var result = System.Text.Json.JsonSerializer.Serialize(new
            {
                status = report.Status.ToString(),
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    description = e.Value.Description,
                    duration = e.Value.Duration.TotalMilliseconds
                }),
                totalDuration = report.TotalDuration.TotalMilliseconds
            });
            await context.Response.WriteAsync(result);
        }
    });

    // Apply EF migrations with proper logging
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<TimeBaseDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        
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

    app.AddTimeBaseEndpoints();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

return 0;
