using TimeBase.Core.Data.Models;
using TimeBase.Core.Infrastructure.Entities;

namespace TimeBase.Core.Data.Services;

/// <summary>
/// Interface for data query coordination and orchestration.
/// </summary>
public interface IDataCoordinator
{
    /// <summary>
    /// Get historical time series data for a symbol.
    /// Strategy: Check database first, fetch from provider if missing.
    /// </summary>
    Task<List<TimeSeriesData>> GetHistoricalAsync(
        string symbol,
        string interval,
        DateTime start,
        DateTime end,
        Guid providerId);

    /// <summary>
    /// Get available providers for a given symbol.
    /// This checks which providers have data for the requested symbol.
    /// </summary>
    Task<List<Provider>> GetProvidersForSymbolAsync(string symbol);

    /// <summary>
    /// Store time series data in the database.
    /// </summary>
    Task<int> StoreTimeSeriesDataAsync(IEnumerable<TimeSeriesData> dataPoints);

    /// <summary>
    /// Get data summary/statistics for a symbol.
    /// </summary>
    Task<DataSummary?> GetDataSummaryAsync(string symbol);
}