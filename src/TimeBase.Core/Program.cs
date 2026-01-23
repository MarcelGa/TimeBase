using Microsoft.EntityFrameworkCore;
using TimeBase.Core.Infrastructure.Data;
using TimeBase.Core.Services;
using TimeBase.Core;
using Serilog;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;

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
                .AddRuntimeInstrumentation();

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
    
    builder.Services.AddServices();
    builder.Services.AddSwaggerGen();

    var app = builder.Build();

    // Add Serilog request logging
    app.UseSerilogRequestLogging();

    // Add Prometheus scraping endpoint
    app.MapPrometheusScrapingEndpoint();

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
