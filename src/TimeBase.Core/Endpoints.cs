namespace TimeBase.Core;

using System;
using System.Collections.Generic;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using TimeBase.Core.Models;
using TimeBase.Core.Services;
using TimeBase.Core.Infrastructure;

public static class EndpointsExtensions
{
    public static IEndpointRouteBuilder AddTimeBaseEndpoints(this IEndpointRouteBuilder endpointRouteBuilder)
    {
        // Get all providers
        endpointRouteBuilder.MapGet("/api/providers", async (ProviderRegistry registry) => 
        {
            var providers = await registry.GetAllProvidersAsync();
            return Results.Ok(new GetProvidersResponse(providers));
        })
        .WithName("GetProviders")
        .WithTags("Providers")
        .Produces<GetProvidersResponse>(200);

        // Get provider by ID
        endpointRouteBuilder.MapGet("/api/providers/{id:guid}", async (Guid id, ProviderRegistry registry) =>
        {
            var provider = await registry.GetProviderByIdAsync(id);
            if (provider == null)
                return Results.NotFound(new ErrorResponse($"Provider {id} not found"));
            
            return Results.Ok(new GetProviderResponse(provider));
        })
        .WithName("GetProvider")
        .WithTags("Providers")
        .Produces<GetProviderResponse>(200)
        .Produces<ErrorResponse>(404);

        // Install a new provider
        endpointRouteBuilder.MapPost("/api/providers", async (
            InstallProviderRequest request,
            ProviderRegistry registry) => 
        {
            try
            {
                var provider = await registry.InstallProviderAsync(request.Repository);
                return Results.Created($"/api/providers/{provider.Id}", 
                    new InstallProviderResponse(
                        "Provider installed successfully",
                        provider
                    ));
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    title: "Failed to install provider",
                    detail: ex.Message,
                    statusCode: 500
                );
            }
        })
        .AddEndpointFilter<ValidationFilter<InstallProviderRequest>>()
        .WithName("InstallProvider")
        .WithTags("Providers")
        .Produces<InstallProviderResponse>(201)
        .ProducesValidationProblem()
        .Produces(500);

        // Uninstall a provider
        endpointRouteBuilder.MapDelete("/api/providers/{id:guid}", async (Guid id, ProviderRegistry registry) => 
        {
            var success = await registry.UninstallProviderAsync(id);
            if (!success)
                return Results.NotFound(new ErrorResponse($"Provider {id} not found"));
            
            return Results.Ok(new UninstallProviderResponse("Provider uninstalled successfully"));
        })
        .WithName("UninstallProvider")
        .WithTags("Providers")
        .Produces<UninstallProviderResponse>(200)
        .Produces<ErrorResponse>(404);

        // Enable/disable a provider
        endpointRouteBuilder.MapPatch("/api/providers/{id:guid}/enabled", async (
            Guid id,
            SetProviderEnabledRequest request,
            ProviderRegistry registry) =>
        {
            var provider = await registry.SetProviderEnabledAsync(id, request.Enabled);
            if (provider == null)
                return Results.NotFound(new ErrorResponse($"Provider {id} not found"));
            
            return Results.Ok(new SetProviderEnabledResponse(
                $"Provider {(provider.Enabled ? "enabled" : "disabled")} successfully",
                provider
            ));
        })
        .AddEndpointFilter<ValidationFilter<SetProviderEnabledRequest>>()
        .WithName("SetProviderEnabled")
        .WithTags("Providers")
        .Produces<SetProviderEnabledResponse>(200)
        .ProducesValidationProblem()
        .Produces<ErrorResponse>(404);

        // Refresh provider capabilities
        endpointRouteBuilder.MapPost("/api/providers/{id:guid}/capabilities", async (
            Guid id,
            ProviderRegistry registry) =>
        {
            var provider = await registry.UpdateCapabilitiesAsync(id);
            if (provider == null)
                return Results.NotFound(new ErrorResponse($"Provider {id} not found"));
            
            var capabilities = registry.GetCachedCapabilities(provider);
            return Results.Ok(new RefreshProviderCapabilitiesResponse(
                "Provider capabilities updated successfully",
                provider,
                capabilities
            ));
        })
        .WithName("RefreshProviderCapabilities")
        .WithTags("Providers")
        .Produces<RefreshProviderCapabilitiesResponse>(200)
        .Produces<ErrorResponse>(404);

        // Refresh all provider capabilities
        endpointRouteBuilder.MapPost("/api/providers/capabilities/refresh", async (ProviderRegistry registry) =>
        {
            await registry.UpdateAllCapabilitiesAsync();
            var providers = await registry.GetAllProvidersAsync(enabled: true);
            
            return Results.Ok(new RefreshAllCapabilitiesResponse(
                "All provider capabilities updated successfully",
                providers.Count,
                providers
            ));
        })
        .WithName("RefreshAllCapabilities")
        .WithTags("Providers")
        .Produces<RefreshAllCapabilitiesResponse>(200);

        // Check provider health
        endpointRouteBuilder.MapGet("/api/providers/{id:guid}/health", async (
            Guid id,
            ProviderRegistry registry,
            IProviderClient providerClient) =>
        {
            var provider = await registry.GetProviderByIdAsync(id);
            if (provider == null)
                return Results.NotFound(new ErrorResponse($"Provider {id} not found"));

            var isHealthy = await providerClient.IsHealthyAsync(provider);
            
            return Results.Ok(new CheckProviderHealthResponse(
                new ProviderHealthInfo(provider.Id, provider.Slug, provider.Name),
                isHealthy,
                DateTime.UtcNow
            ));
        })
        .WithName("CheckProviderHealth")
        .WithTags("Providers")
        .Produces<CheckProviderHealthResponse>(200)
        .Produces<ErrorResponse>(404);

        // Get historical data
        endpointRouteBuilder.MapGet("/api/data/{symbol}", async (
            [AsParameters] GetHistoricalDataRequest request,
            DataCoordinator coordinator) => 
        {
            // Default interval if not provided
            var interval = request.Interval ?? "1d";

            // Default to last 30 days if no dates provided
            // Ensure DateTimes are UTC (PostgreSQL requires it)
            var startDate = request.Start.HasValue 
                ? DateTime.SpecifyKind(request.Start.Value, DateTimeKind.Utc)
                : DateTime.UtcNow.AddDays(-30);
            var endDate = request.End.HasValue
                ? DateTime.SpecifyKind(request.End.Value, DateTimeKind.Utc)
                : DateTime.UtcNow;

            try
            {
                var data = await coordinator.GetHistoricalAsync(request.Symbol, interval, startDate, endDate, request.ProviderId);
                return Results.Ok(new GetHistoricalDataResponse(
                    request.Symbol,
                    interval,
                    startDate,
                    endDate,
                    data.Count,
                    data
                ));
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    title: "Failed to fetch data",
                    detail: ex.Message,
                    statusCode: 500
                );
            }
        })
        .AddEndpointFilter<ValidationFilter<GetHistoricalDataRequest>>()
        .WithName("GetHistoricalData")
        .WithTags("Data")
        .Produces<GetHistoricalDataResponse>(200)
        .ProducesValidationProblem()
        .Produces(500);

        // Get data summary for a symbol
        endpointRouteBuilder.MapGet("/api/data/{symbol}/summary", async (string symbol, DataCoordinator coordinator) =>
        {
            var summary = await coordinator.GetDataSummaryAsync(symbol);
            if (summary == null)
                return Results.NotFound(new ErrorResponse($"No data found for symbol {symbol}"));
            
            return Results.Ok(new GetDataSummaryResponse(summary));
        })
        .WithName("GetDataSummary")
        .WithTags("Data")
        .Produces<GetDataSummaryResponse>(200)
        .Produces<ErrorResponse>(404);

        // Get available providers for a symbol
        endpointRouteBuilder.MapGet("/api/data/{symbol}/providers", async (string symbol, DataCoordinator coordinator) =>
        {
            var providers = await coordinator.GetProvidersForSymbolAsync(symbol);
            return Results.Ok(new GetProvidersForSymbolResponse(
                symbol,
                providers.Count,
                providers
            ));
        })
        .WithName("GetProvidersForSymbol")
        .WithTags("Data")
        .Produces<GetProvidersForSymbolResponse>(200);

        return endpointRouteBuilder;
    }
}
