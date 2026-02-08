namespace TimeBase.Core.Data;

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

        return endpointRouteBuilder;
    }
}