namespace TimeBase.Core;

using System;
using System.Collections.Generic;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using TimeBase.Core.Models;
using TimeBase.Core.Services;

public static class EndpointsExtensions
{
    public static IEndpointRouteBuilder AddTimeBaseEndpoints(this IEndpointRouteBuilder endpointRouteBuilder)
    {
        // Get all providers
        endpointRouteBuilder.MapGet("/api/providers", async (ProviderRegistry registry) => 
        {
            var providers = await registry.GetAllProvidersAsync();
            return Results.Ok(new { providers });
        })
        .WithName("GetProviders")
        .WithTags("Providers")
        .Produces<object>(200);

        // Get provider by ID
        endpointRouteBuilder.MapGet("/api/providers/{id:guid}", async (Guid id, ProviderRegistry registry) =>
        {
            var provider = await registry.GetProviderByIdAsync(id);
            if (provider == null)
                return Results.NotFound(new { error = $"Provider {id} not found" });
            
            return Results.Ok(new { provider });
        })
        .WithName("GetProvider")
        .WithTags("Providers")
        .Produces<object>(200)
        .Produces(404);

        // Install a new provider
        endpointRouteBuilder.MapPost("/api/providers", async (
            InstallProviderRequest request,
            IValidator<InstallProviderRequest> validator,
            ProviderRegistry registry) => 
        {
            var validationResult = await validator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(e => e.ErrorMessage).ToArray()
                    );
                return Results.ValidationProblem(errors);
            }
            
            try
            {
                var provider = await registry.InstallProviderAsync(request.Repository);
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
        })
        .WithName("InstallProvider")
        .WithTags("Providers")
        .Produces<object>(201)
        .ProducesValidationProblem()
        .Produces(500);

        // Uninstall a provider
        endpointRouteBuilder.MapDelete("/api/providers/{id:guid}", async (Guid id, ProviderRegistry registry) => 
        {
            var success = await registry.UninstallProviderAsync(id);
            if (!success)
                return Results.NotFound(new { error = $"Provider {id} not found" });
            
            return Results.Ok(new { message = "Provider uninstalled successfully" });
        })
        .WithName("UninstallProvider")
        .WithTags("Providers")
        .Produces<object>(200)
        .Produces(404);

        // Enable/disable a provider
        endpointRouteBuilder.MapPatch("/api/providers/{id:guid}/enabled", async (
            Guid id,
            SetProviderEnabledRequest request,
            IValidator<SetProviderEnabledRequest> validator,
            ProviderRegistry registry) =>
        {
            var validationResult = await validator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(e => e.ErrorMessage).ToArray()
                    );
                return Results.ValidationProblem(errors);
            }
            
            var provider = await registry.SetProviderEnabledAsync(id, request.Enabled);
            if (provider == null)
                return Results.NotFound(new { error = $"Provider {id} not found" });
            
            return Results.Ok(new 
            { 
                message = $"Provider {(provider.Enabled ? "enabled" : "disabled")} successfully",
                provider
            });
        })
        .WithName("SetProviderEnabled")
        .WithTags("Providers")
        .Produces<object>(200)
        .ProducesValidationProblem()
        .Produces(404);

        // Get historical data
        endpointRouteBuilder.MapGet("/api/data/{symbol}", async (
            string symbol,
            string? interval,
            DateTime? start,
            DateTime? end,
            Guid? providerId,
            IValidator<GetHistoricalDataRequest> validator,
            DataCoordinator coordinator) => 
        {
            // Default interval if not provided
            interval ??= "1d";
            
            var request = new GetHistoricalDataRequest(symbol, interval, start, end, providerId);
            var validationResult = await validator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(e => e.ErrorMessage).ToArray()
                    );
                return Results.ValidationProblem(errors);
            }

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
        })
        .WithName("GetHistoricalData")
        .WithTags("Data")
        .Produces<object>(200)
        .ProducesValidationProblem()
        .Produces(500);

        // Get data summary for a symbol
        endpointRouteBuilder.MapGet("/api/data/{symbol}/summary", async (string symbol, DataCoordinator coordinator) =>
        {
            var summary = await coordinator.GetDataSummaryAsync(symbol);
            if (summary == null)
                return Results.NotFound(new { error = $"No data found for symbol {symbol}" });
            
            return Results.Ok(new { summary });
        })
        .WithName("GetDataSummary")
        .WithTags("Data")
        .Produces<object>(200)
        .Produces(404);

        // Get available providers for a symbol
        endpointRouteBuilder.MapGet("/api/data/{symbol}/providers", async (string symbol, DataCoordinator coordinator) =>
        {
            var providers = await coordinator.GetProvidersForSymbolAsync(symbol);
            return Results.Ok(new 
            { 
                symbol,
                count = providers.Count,
                providers
            });
        })
        .WithName("GetProvidersForSymbol")
        .WithTags("Data")
        .Produces<object>(200);

        return endpointRouteBuilder;
    }
}
