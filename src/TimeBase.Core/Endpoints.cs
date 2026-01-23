namespace TimeBase.Core;

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using TimeBase.Core.Data;
using TimeBase.Core.Services;
using Microsoft.EntityFrameworkCore;

public static class EndpointsExtensions
{
    public static void AddTimeBaseEndpoints(this WebApplication app)
    {
        app.MapGet("/health", () => Results.Json(new { status = "healthy" }));

        app.MapGet("/api/providers", async (TimeBaseDbContext db) => {
            var list = await db.Providers.ToListAsync();
            return Results.Ok(new { providers = list });
        });

        app.MapPost("/api/providers", async (ProviderRegistry registry, HttpRequest request) => {
            var payload = await request.ReadFromJsonAsync<Dictionary<string, string>>();
            if (payload == null || !payload.ContainsKey("repository"))
                return Results.BadRequest(new { error = "repository is required" });
            await registry.InstallProviderAsync(payload["repository"]);
            return Results.Json(new Dictionary<string, string> { { "message", "Provider installation started" } });
        });

        app.MapDelete("/api/providers/{id}", (string id) => {
            return Results.Ok(new { message = $"Provider {id} uninstall not implemented in MVP" });
        });

        app.MapGet("/api/data/{symbol}", (string symbol, string interval) => {
            return Results.Ok(new { symbol, interval, data = new object[0] });
        });
    }
}
