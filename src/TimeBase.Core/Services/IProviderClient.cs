using System.Threading.Channels;

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
    Task<List<TimeSeriesData>> GetHistoricalDataAsync(
        Provider provider,
        string symbol,
        string interval,
        DateTime start,
        DateTime end);

    /// <summary>
    /// Get provider capabilities.
    /// </summary>
    /// <param name="provider">The provider to query</param>
    /// <returns>Provider capabilities or null if provider is unreachable</returns>
    Task<ProviderCapabilities?> GetCapabilitiesAsync(Provider provider);

    /// <summary>
    /// Check if a provider is healthy and reachable.
    /// </summary>
    /// <param name="provider">The provider to check</param>
    /// <returns>True if healthy, false otherwise</returns>
    Task<bool> IsHealthyAsync(Provider provider);

    /// <summary>
    /// Stream real-time data from a provider via bidirectional gRPC streaming.
    /// </summary>
    /// <param name="provider">The provider to stream from</param>
    /// <param name="controlChannel">Channel for sending subscription control messages</param>
    /// <param name="cancellationToken">Cancellation token to stop the stream</param>
    /// <returns>Async enumerable of time series data points</returns>
    IAsyncEnumerable<TimeSeriesData> StreamRealTimeDataAsync(
        Provider provider,
        ChannelReader<StreamControlMessage> controlChannel,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Stream control message for managing real-time subscriptions.
/// </summary>
public record StreamControlMessage(
    StreamControlAction Action,
    string Symbol,
    string Interval,
    Dictionary<string, string>? Options = null
);

/// <summary>
/// Actions for controlling real-time stream subscriptions.
/// </summary>
public enum StreamControlAction
{
    Subscribe = 0,
    Unsubscribe = 1,
    Pause = 2,
    Resume = 3
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