namespace TimeBase.Core;

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using TimeBase.Core.Services;

public static class EndpointsExtensions
{
    public static void AddTimeBaseEndpoints(this WebApplication app)
    {
        // Health check endpoint
        app.MapGet("/health", () => Results.Ok(new 
        { 
            status = "healthy",
            timestamp = DateTime.UtcNow 
        }));

        // Get all providers
        app.MapGet("/api/providers", async (ProviderRegistry registry) => 
        {
            var providers = await registry.GetAllProvidersAsync();
            return Results.Ok(new { providers });
        });

        // Get provider by ID
        app.MapGet("/api/providers/{id:guid}", async (Guid id, ProviderRegistry registry) =>
        {
            var provider = await registry.GetProviderByIdAsync(id);
            if (provider == null)
                return Results.NotFound(new { error = $"Provider {id} not found" });
            
            return Results.Ok(new { provider });
        });

        // Install a new provider
        app.MapPost("/api/providers", async (ProviderRegistry registry, HttpRequest request) => 
        {
            var payload = await request.ReadFromJsonAsync<Dictionary<string, string>>();
            if (payload == null || !payload.ContainsKey("repository"))
                return Results.BadRequest(new { error = "repository field is required" });
            
            try
            {
                var provider = await registry.InstallProviderAsync(payload["repository"]);
                return Results.Created($"/api/providers/{provider.Id}", new 
                { 
                    message = "Provider installed successfully",
                    provider
                });
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    title: "Failed to install provider",
                    detail: ex.Message,
                    statusCode: 500
                );
            }
        });

        // Uninstall a provider
        app.MapDelete("/api/providers/{id:guid}", async (Guid id, ProviderRegistry registry) => 
        {
            var success = await registry.UninstallProviderAsync(id);
            if (!success)
                return Results.NotFound(new { error = $"Provider {id} not found" });
            
            return Results.Ok(new { message = "Provider uninstalled successfully" });
        });

        // Enable/disable a provider
        app.MapPatch("/api/providers/{id:guid}/enabled", async (
            Guid id, 
            ProviderRegistry registry,
            HttpRequest request) =>
        {
            var payload = await request.ReadFromJsonAsync<Dictionary<string, bool>>();
            if (payload == null || !payload.ContainsKey("enabled"))
                return Results.BadRequest(new { error = "enabled field is required" });
            
            var provider = await registry.SetProviderEnabledAsync(id, payload["enabled"]);
            if (provider == null)
                return Results.NotFound(new { error = $"Provider {id} not found" });
            
            return Results.Ok(new 
            { 
                message = $"Provider {(provider.Enabled ? "enabled" : "disabled")} successfully",
                provider
            });
        });

        // Get historical data (placeholder)
        app.MapGet("/api/data/{symbol}", (string symbol, string interval) => 
        {
            return Results.Ok(new 
            { 
                symbol, 
                interval, 
                data = Array.Empty<object>(),
                message = "Data retrieval not yet implemented in Phase 2"
            });
        });
    }
}
