namespace TimeBase.Core;

using System;
using System.Collections.Generic;

using FluentValidation;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

using TimeBase.Core.Infrastructure;
using TimeBase.Core.Models;
using TimeBase.Core.Services;

public static class EndpointsExtensions
{
    /// <summary>
    /// Adds TimeBase API endpoints. Can optionally use a route group for applying global filters.
    /// </summary>
    /// <param name="endpointRouteBuilder">The main endpoint route builder (app)</param>
    /// <param name="apiGroup">Optional API route group with global validation filter applied</param>
    public static IEndpointRouteBuilder AddTimeBaseEndpoints(
        this IEndpointRouteBuilder endpointRouteBuilder,
        IEndpointRouteBuilder? apiGroup = null)
    {
        // Use the API group if provided, otherwise use the main route builder
        var builder = apiGroup ?? endpointRouteBuilder;

        // Get all providers
        builder.MapGet("/providers", async (IProviderRegistry registry) =>
        {
            var providers = await registry.GetAllProvidersAsync();
            return Results.Ok(new GetProvidersResponse(providers));
        })
        .WithName("GetProviders")
        .WithTags("Providers")
        .Produces<GetProvidersResponse>(200);

        // Get provider by ID
        builder.MapGet("/providers/{id:guid}", async (Guid id, IProviderRegistry registry) =>
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
        builder.MapPost("/providers", async (
            InstallProviderRequest request,
            IProviderRegistry registry) =>
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
        .WithName("InstallProvider")
        .WithTags("Providers")
        .Produces<InstallProviderResponse>(201)
        .ProducesValidationProblem()
        .Produces(500);

        // Uninstall a provider
        builder.MapDelete("/providers/{id:guid}", async (Guid id, IProviderRegistry registry) =>
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
        builder.MapPatch("/providers/{id:guid}/enabled", async (
            Guid id,
            SetProviderEnabledRequest request,
            IProviderRegistry registry) =>
        {
            var provider = await registry.SetProviderEnabledAsync(id, request.Enabled);
            if (provider == null)
                return Results.NotFound(new ErrorResponse($"Provider {id} not found"));

            return Results.Ok(new SetProviderEnabledResponse(
                $"Provider {(provider.Enabled ? "enabled" : "disabled")} successfully",
                provider
            ));
        })
        .WithName("SetProviderEnabled")
        .WithTags("Providers")
        .Produces<SetProviderEnabledResponse>(200)
        .ProducesValidationProblem()
        .Produces<ErrorResponse>(404);

        // Refresh provider capabilities
        builder.MapPost("/providers/{id:guid}/capabilities", async (
            Guid id,
            IProviderRegistry registry) =>
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
        builder.MapPost("/providers/capabilities/refresh", async (IProviderRegistry registry) =>
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
        builder.MapGet("/providers/{id:guid}/health", async (
            Guid id,
            IProviderRegistry registry,
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
        builder.MapGet("/data/{symbol}", async (
            [AsParameters] GetHistoricalDataRequest request,
            IDataCoordinator coordinator) =>
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
        .WithName("GetHistoricalData")
        .WithTags("Data")
        .Produces<GetHistoricalDataResponse>(200)
        .ProducesValidationProblem()
        .Produces(500);

        // Get data summary for a symbol
        builder.MapGet("/data/{symbol}/summary", async (string symbol, IDataCoordinator coordinator) =>
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
        builder.MapGet("/data/{symbol}/providers", async (string symbol, IDataCoordinator coordinator) =>
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