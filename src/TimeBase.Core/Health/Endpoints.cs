namespace TimeBase.Core.Health;

public static class Endpoints
{
    public static IEndpointRouteBuilder AddHealthCheckEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = _ => false, // No checks, just returns that the app is running
            AllowCachingResponses = false
        });

        app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"), // Only checks tagged with "ready"
            AllowCachingResponses = false,
            ResponseWriter = WriteHealthCheckResponse
        });

        app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = _ => true, // All health checks
            AllowCachingResponses = false,
            ResponseWriter = WriteHealthCheckResponse
        });

        return app;
    }

    private static async Task WriteHealthCheckResponse(HttpContext context, Microsoft.Extensions.Diagnostics.HealthChecks.HealthReport report)
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
}