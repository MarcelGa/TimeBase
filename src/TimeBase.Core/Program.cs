using TimeBase.Core.Services;
using TimeBase.Core;
using Serilog;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using FluentValidation;
using AspNetCoreRateLimit;

using TimeBase.Core.Health;
using TimeBase.Core.Infrastructure;

// Configure Serilog early
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

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

builder.Services.AddHealthChecks(out IHealthChecksBuilder healthChecks);
builder.Services.AddInfrastructure(builder.Configuration, healthChecks);

var app = builder.Build();

// Add Serilog request logging
app.UseSerilogRequestLogging();

// Add rate limiting
app.UseIpRateLimiting();

// Use infrastructure (e.g. apply migrations)
app.Services.UseInfrastructure();

// Add endpoints
app
    .AddHealthCheckEndpoints()
    .AddTimeBaseEndpoints()
    .MapPrometheusScrapingEndpoint();


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Run();

// Make Program class accessible to integration tests
public partial class Program { }
