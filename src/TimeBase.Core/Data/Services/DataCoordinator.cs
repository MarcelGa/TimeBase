using System.Diagnostics;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using TimeBase.Core.Data.Models;
using TimeBase.Core.Infrastructure.Data;
using TimeBase.Core.Infrastructure.Entities;
using TimeBase.Core.Providers.Services;
using TimeBase.Core.Shared.Services;

namespace TimeBase.Core.Data.Services;

/// <summary>
/// DataCoordinator handles data queries and coordinates with providers.
/// Checks database first, then fetches from providers via gRPC if needed.
/// </summary>
public class DataCoordinator(
    TimeBaseDbContext db,
    IProviderRegistry providerRegistry,
    IProviderClient providerClient,
    ILogger<DataCoordinator> logger,
    ITimeBaseMetrics metrics) : IDataCoordinator
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
        Guid providerId)
    {
        logger.LogInformation(
            "Fetching historical data for {Symbol} with interval {Interval} from {Start} to {End} from provider {ProviderId}",
            symbol, interval, start, end, providerId);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            // 1. Validate provider exists and is enabled
            var provider = await providerRegistry.GetProviderByIdAsync(providerId);
            if (provider == null)
            {
                logger.LogWarning("Provider {ProviderId} not found", providerId);
                return new List<TimeSeriesData>();
            }

            if (!provider.Enabled)
            {
                logger.LogWarning("Provider {ProviderId} ({Slug}) is disabled", providerId, provider.Slug);
                return new List<TimeSeriesData>();
            }

            // 2. Check database first for this specific provider
            var data = await db.TimeSeries
                .Where(d => d.Symbol == symbol && d.Interval == interval)
                .Where(d => d.Time >= start && d.Time <= end)
                .Where(d => d.ProviderId == providerId)
                .OrderBy(d => d.Time)
                .ToListAsync();

            // 3. If data exists in database, return it
            if (data.Count > 0)
            {
                metrics.RecordDataQuery(symbol, interval, data.Count, stopwatch.Elapsed.TotalMilliseconds, success: true);
                logger.LogInformation("Fetched {Count} data points for {Symbol} from database (provider: {Provider})",
                    data.Count, symbol, provider.Slug);
                return data;
            }

            // 4. No data in database - fetch from provider
            logger.LogInformation("No data in database for {Symbol}, fetching from provider {Provider}",
                symbol, provider.Slug);

            var providerData = await providerClient.GetHistoricalDataAsync(
                provider, symbol, interval, start, end);

            if (providerData.Count == 0)
            {
                logger.LogInformation("Provider {Provider} returned no data for {Symbol}", provider.Slug, symbol);
                return new List<TimeSeriesData>();
            }

            logger.LogInformation("Provider {Provider} returned {Count} data points for {Symbol}",
                provider.Slug, providerData.Count, symbol);

            // 5. Store fetched data in database for future queries
            await StoreTimeSeriesDataAsync(providerData);

            metrics.RecordDataQuery(symbol, interval, providerData.Count, stopwatch.Elapsed.TotalMilliseconds, success: true);

            logger.LogInformation(
                "Fetched and stored {Count} data points for {Symbol} from provider {Provider}",
                providerData.Count, symbol, provider.Slug);

            return providerData;
        }
        catch (Exception ex)
        {
            metrics.RecordDataQuery(symbol, interval, 0, stopwatch.Elapsed.TotalMilliseconds, success: false);
            metrics.RecordError("data_query", ex.GetType().Name);
            logger.LogError(ex, "Failed to fetch data for {Symbol} from provider {ProviderId}", symbol, providerId);
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

        var providers = await db.Providers
            .Where(p => providerIds.Contains(p.Id) && p.Enabled)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync();

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
        var summary = await db.TimeSeries
            .Where(d => d.Symbol == symbol)
            .GroupBy(d => d.Symbol)
            .Select(g => new DataSummary(
                g.Key,
                g.Count(),
                g.Min(d => d.Time),
                g.Max(d => d.Time),
                g.Select(d => d.ProviderId).Distinct().Count(),
                g.Select(d => d.Interval).Distinct().ToList()
            ))
            .FirstOrDefaultAsync();

        return summary;
    }
}