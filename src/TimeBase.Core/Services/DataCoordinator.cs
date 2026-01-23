using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TimeBase.Core.Infrastructure.Entities;
using TimeBase.Core.Infrastructure.Data;

namespace TimeBase.Core.Services;

/// <summary>
/// DataCoordinator handles data queries and coordinates with providers.
/// In MVP: Queries TimescaleDB directly.
/// Future: Routes requests to providers via gRPC, maintains connection pool.
/// </summary>
public class DataCoordinator(TimeBaseDbContext db, ProviderRegistry providerRegistry, ILogger<DataCoordinator> logger)
{
    /// <summary>
    /// Get historical time series data for a symbol.
    /// </summary>
    public async Task<List<TimeSeriesData>> GetHistoricalAsync(
        string symbol, 
        string interval, 
        DateTime start, 
        DateTime end,
        Guid? providerId = null)
    {
        logger.LogInformation(
            "Fetching historical data for {Symbol} with interval {Interval} from {Start} to {End}",
            symbol, interval, start, end);

        var query = db.TimeSeries
            .Where(d => d.Symbol == symbol && d.Interval == interval)
            .Where(d => d.Time >= start && d.Time <= end);

        // Optionally filter by provider
        if (providerId.HasValue)
        {
            query = query.Where(d => d.ProviderId == providerId.Value);
        }

        var data = await query
            .OrderBy(d => d.Time)
            .ToListAsync();

        logger.LogInformation("Fetched {Count} data points for {Symbol}", data.Count, symbol);
        return data;
    }

    /// <summary>
    /// Get available providers for a given symbol.
    /// This checks which providers have data for the requested symbol.
    /// </summary>
    public async Task<List<Provider>> GetProvidersForSymbolAsync(string symbol)
    {
        logger.LogInformation("Finding providers for symbol {Symbol}", symbol);

        var providerIds = await db.TimeSeries
            .Where(d => d.Symbol == symbol)
            .Select(d => d.ProviderId)
            .Distinct()
            .ToListAsync();

        if (!providerIds.Any())
        {
            logger.LogInformation("No providers found with data for {Symbol}", symbol);
            return new List<Provider>();
        }

        var providers = new List<Provider>();
        foreach (var id in providerIds)
        {
            var provider = await providerRegistry.GetProviderByIdAsync(id);
            if (provider != null && provider.Enabled)
            {
                providers.Add(provider);
            }
        }

        logger.LogInformation("Found {Count} providers for {Symbol}", providers.Count, symbol);
        return providers;
    }

    /// <summary>
    /// Store time series data in the database.
    /// Future: This will be called when ingesting data from providers.
    /// </summary>
    public async Task<int> StoreTimeSeriesDataAsync(IEnumerable<TimeSeriesData> dataPoints)
    {
        var list = dataPoints.ToList();
        if (!list.Any())
        {
            return 0;
        }

        logger.LogInformation("Storing {Count} time series data points", list.Count);

        db.TimeSeries.AddRange(list);
        await db.SaveChangesAsync();

        logger.LogInformation("Successfully stored {Count} data points", list.Count);
        return list.Count;
    }

    /// <summary>
    /// Get data summary/statistics for a symbol.
    /// </summary>
    public async Task<DataSummary?> GetDataSummaryAsync(string symbol)
    {
        var data = await db.TimeSeries
            .Where(d => d.Symbol == symbol)
            .ToListAsync();

        if (!data.Any())
        {
            return null;
        }

        return new DataSummary(
            Symbol: symbol,
            TotalDataPoints: data.Count,
            EarliestDate: data.Min(d => d.Time),
            LatestDate: data.Max(d => d.Time),
            Providers: data.Select(d => d.ProviderId).Distinct().Count(),
            Intervals: data.Select(d => d.Interval).Distinct().ToList()
        );
    }
}

/// <summary>
/// Summary information about available data for a symbol.
/// </summary>
public record DataSummary(
    string Symbol,
    int TotalDataPoints,
    DateTime EarliestDate,
    DateTime LatestDate,
    int Providers,
    List<string> Intervals
);
