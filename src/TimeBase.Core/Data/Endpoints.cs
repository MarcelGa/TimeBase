namespace TimeBase.Core.Data;

using System;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

using TimeBase.Core.Data.Models;
using TimeBase.Core.Data.Services;
using TimeBase.Core.Shared.Models;

public static class DataEndpoints
{
    /// <summary>
    /// Adds data query endpoints.
    /// </summary>
    public static IEndpointRouteBuilder AddDataEndpoints(
        this IEndpointRouteBuilder endpointRouteBuilder,
        IEndpointRouteBuilder? apiGroup = null)
    {
        // Use the API group if provided, otherwise use the main route builder
        var builder = apiGroup ?? endpointRouteBuilder;

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

        // Get historical data for a symbol from a specific provider
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
                var data = await coordinator.GetHistoricalAsync(request.Symbol, interval, startDate, endDate, request.Provider);
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

        return endpointRouteBuilder;
    }
}