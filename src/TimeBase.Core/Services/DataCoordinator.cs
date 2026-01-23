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
public class DataCoordinator(
    TimeBaseDbContext db, 
    ProviderRegistry providerRegistry, 
    ILogger<DataCoordinator> logger,
    TimeBaseMetrics metrics)
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

        var startTime = DateTime.UtcNow;
        try
        {
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

            var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
            metrics.RecordDataQuery(symbol, interval, data.Count, duration, success: true);

            logger.LogInformation("Fetched {Count} data points for {Symbol}", data.Count, symbol);
            return data;
        }
        catch (Exception ex)
        {
            var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
            metrics.RecordDataQuery(symbol, interval, 0, duration, success: false);
            metrics.RecordError("data_query", ex.GetType().Name);
            logger.LogError(ex, "Failed to fetch data for {Symbol}", symbol);
            throw;
        }
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

        try
        {
            db.TimeSeries.AddRange(list);
            await db.SaveChangesAsync();

            // Record metrics - extract symbol if all points have same symbol
            var symbol = list.Select(d => d.Symbol).Distinct().Count() == 1 
                ? list.First().Symbol 
                : null;
            metrics.RecordDataStore(list.Count, symbol, success: true);

            logger.LogInformation("Successfully stored {Count} data points", list.Count);
            return list.Count;
        }
        catch (Exception ex)
        {
            metrics.RecordError("data_store", ex.GetType().Name);
            logger.LogError(ex, "Failed to store time series data");
            throw;
        }
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
