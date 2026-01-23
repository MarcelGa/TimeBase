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

        // Get historical data
        app.MapGet("/api/data/{symbol}", async (
            string symbol, 
            string interval,
            DateTime? start,
            DateTime? end,
            Guid? providerId,
            DataCoordinator coordinator) => 
        {
            // Default to last 30 days if no dates provided
            var startDate = start ?? DateTime.UtcNow.AddDays(-30);
            var endDate = end ?? DateTime.UtcNow;

            try
            {
                var data = await coordinator.GetHistoricalAsync(symbol, interval, startDate, endDate, providerId);
                return Results.Ok(new 
                { 
                    symbol,
                    interval,
                    start = startDate,
                    end = endDate,
                    count = data.Count,
                    data
                });
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    title: "Failed to fetch data",
                    detail: ex.Message,
                    statusCode: 500
                );
            }
        });

        // Get data summary for a symbol
        app.MapGet("/api/data/{symbol}/summary", async (string symbol, DataCoordinator coordinator) =>
        {
            var summary = await coordinator.GetDataSummaryAsync(symbol);
            if (summary == null)
                return Results.NotFound(new { error = $"No data found for symbol {symbol}" });
            
            return Results.Ok(new { summary });
        });

        // Get available providers for a symbol
        app.MapGet("/api/data/{symbol}/providers", async (string symbol, DataCoordinator coordinator) =>
        {
            var providers = await coordinator.GetProvidersForSymbolAsync(symbol);
            return Results.Ok(new 
            { 
                symbol,
                count = providers.Count,
                providers
            });
        });
    }
}
