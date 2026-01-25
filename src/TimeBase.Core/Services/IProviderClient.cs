using TimeBase.Core.Infrastructure.Entities;

namespace TimeBase.Core.Services;

/// <summary>
/// Interface for communicating with data providers via gRPC.
/// </summary>
public interface IProviderClient
{
    /// <summary>
    /// Fetch historical time series data from a provider.
    /// </summary>
    /// <param name="provider">The provider to fetch from</param>
    /// <param name="symbol">Financial symbol (e.g., "AAPL")</param>
    /// <param name="interval">Time interval (e.g., "1d")</param>
    /// <param name="start">Start date/time</param>
    /// <param name="end">End date/time</param>
    /// <returns>List of time series data points</returns>
    Task<List<Infrastructure.Entities.TimeSeriesData>> GetHistoricalDataAsync(
        Infrastructure.Entities.Provider provider,
        string symbol,
        string interval,
        DateTime start,
        DateTime end);

    /// <summary>
    /// Get provider capabilities.
    /// </summary>
    /// <param name="provider">The provider to query</param>
    /// <returns>Provider capabilities or null if provider is unreachable</returns>
    Task<ProviderCapabilities?> GetCapabilitiesAsync(Infrastructure.Entities.Provider provider);

    /// <summary>
    /// Check if a provider is healthy and reachable.
    /// </summary>
    /// <param name="provider">The provider to check</param>
    /// <returns>True if healthy, false otherwise</returns>
    Task<bool> IsHealthyAsync(Infrastructure.Entities.Provider provider);
}

/// <summary>
/// Provider capabilities returned from gRPC.
/// </summary>
public record ProviderCapabilities(
    string Name,
    string Version,
    string Slug,
    bool SupportsHistorical,
    bool SupportsRealtime,
    List<string> DataTypes,
    List<string> Intervals
);
