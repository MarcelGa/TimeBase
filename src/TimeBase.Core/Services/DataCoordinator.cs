using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TimeBase.Core.Infrastructure.Entities;
using TimeBase.Core.Infrastructure.Data;

namespace TimeBase.Core.Services;

/// <summary>
/// DataCoordinator handles data queries and coordinates with providers.
/// Checks database first, then fetches from providers via gRPC if needed.
/// </summary>
public class DataCoordinator(
    TimeBaseDbContext db, 
    ProviderRegistry providerRegistry,
    IProviderClient providerClient,
    ILogger<DataCoordinator> logger,
    ITimeBaseMetrics metrics)
{
    /// <summary>
    /// Get historical time series data for a symbol.
    /// Strategy: Check database first, fetch from provider if missing.
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
            // 1. Check database first
            var query = db.TimeSeries
                .Where(d => d.Symbol == symbol && d.Interval == interval)
                .Where(d => d.Time >= start && d.Time <= end);

            if (providerId.HasValue)
            {
                query = query.Where(d => d.ProviderId == providerId.Value);
            }

            var data = await query
                .OrderBy(d => d.Time)
                .ToListAsync();

            // 2. If data exists in database, return it
            if (data.Count > 0)
            {
                var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
                metrics.RecordDataQuery(symbol, interval, data.Count, duration, success: true);
                logger.LogInformation("Fetched {Count} data points for {Symbol} from database", data.Count, symbol);
                return data;
            }

            // 3. No data in database - fetch from provider
            logger.LogInformation("No data in database for {Symbol}, fetching from provider", symbol);

            // Get providers to fetch from
            List<Provider> providersToTry;
            if (providerId.HasValue)
            {
                var provider = await providerRegistry.GetProviderByIdAsync(providerId.Value);
                if (provider == null || !provider.Enabled)
                {
                    logger.LogWarning("Provider {ProviderId} not found or disabled", providerId);
                    return new List<TimeSeriesData>();
                }
                providersToTry = [provider];
            }
            else
            {
                // Get all enabled providers
                providersToTry = await providerRegistry.GetAllProvidersAsync(enabled: true);
                if (providersToTry.Count == 0)
                {
                    logger.LogWarning("No enabled providers available");
                    return new List<TimeSeriesData>();
                }
            }

            // 4. Try each provider until one returns data
            List<TimeSeriesData> providerData = [];
            foreach (var provider in providersToTry)
            {
                logger.LogDebug("Trying provider {Provider} for {Symbol}", provider.Slug, symbol);
                
                providerData = await providerClient.GetHistoricalDataAsync(
                    provider, symbol, interval, start, end);

                if (providerData.Count > 0)
                {
                    logger.LogInformation("Provider {Provider} returned {Count} data points for {Symbol}", 
                        provider.Slug, providerData.Count, symbol);
                    break;
                }
                
                logger.LogDebug("Provider {Provider} returned no data for {Symbol}", provider.Slug, symbol);
            }

            if (providerData.Count == 0)
            {
                logger.LogInformation("No providers returned data for {Symbol}", symbol);
                return new List<TimeSeriesData>();
            }

            // 5. Store fetched data in database for future queries
            await StoreTimeSeriesDataAsync(providerData);

            var totalDuration = (DateTime.UtcNow - startTime).TotalMilliseconds;
            metrics.RecordDataQuery(symbol, interval, providerData.Count, totalDuration, success: true);

            logger.LogInformation(
                "Fetched and stored {Count} data points for {Symbol}",
                providerData.Count, symbol);

            return providerData;
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
