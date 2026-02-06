using AspNetCoreRateLimit;

using FluentValidation;

using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Serilog;

using TimeBase.Core;
using TimeBase.Core.Endpoints;
using TimeBase.Core.Health;
using TimeBase.Core.Hubs;
using TimeBase.Core.Infrastructure;
using TimeBase.Core.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog early (skip in test environments to avoid frozen logger issues)
if (builder.Environment.EnvironmentName != "Testing")
{
    // Replace default logging with Serilog
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());
}

// Add CORS for development
builder.Services.AddCors(options =>
{
    options.AddPolicy("Development", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:5173")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials(); // Required for SignalR
    });

    options.AddPolicy("Production", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? Array.Empty<string>();
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Add SignalR
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
});

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

// Add FluentValidation (automatic validator registration)
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Add global validation filter (automatically validates all requests with registered validators)
builder.Services.AddSingleton<GlobalValidationFilter>();

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

// Add Serilog request logging (skip in test environments)
if (app.Environment.EnvironmentName != "Testing")
{
    app.UseSerilogRequestLogging(options =>
    {
        // Exclude health check endpoints from request logging to reduce log noise
        options.GetLevel = (httpContext, elapsed, ex) =>
        {
            if (httpContext.Request.Path.StartsWithSegments("/health"))
                return Serilog.Events.LogEventLevel.Verbose;

            return Serilog.Events.LogEventLevel.Information;
        };
    });
}

// Enable CORS
if (app.Environment.IsDevelopment())
{
    app.UseCors("Development");
}
else
{
    app.UseCors("Production");
}

// Add rate limiting
app.UseIpRateLimiting();

// Use infrastructure (e.g. apply migrations)
app.Services.UseInfrastructure();

// Create API route group with global validation filter
var api = app.MapGroup("/api")
    .AddEndpointFilter<GlobalValidationFilter>();

// Add endpoints (validation is applied automatically via the route group filter)
app
    .AddHealthCheckEndpoints()
    .AddProviderEndpoints(api)  // Provider management endpoints
    .AddDataEndpoints(api)      // Data query endpoints
    .MapPrometheusScrapingEndpoint();

// Map SignalR hub
app.MapHub<MarketHub>("/hubs/market");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Run();

// Make Program class accessible to integration tests
public partial class Program { }