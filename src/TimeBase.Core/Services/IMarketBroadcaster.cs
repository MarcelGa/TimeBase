using TimeBase.Core.Infrastructure.Entities;

namespace TimeBase.Core.Services;

/// <summary>
/// Service for broadcasting real-time market data updates to connected clients.
/// </summary>
public interface IMarketBroadcaster
{
    /// <summary>
    /// Broadcast a price update to all clients subscribed to the symbol.
    /// </summary>
    Task BroadcastPriceUpdateAsync(TimeSeriesData data);

    /// <summary>
    /// Broadcast price updates for multiple data points.
    /// </summary>
    Task BroadcastPriceUpdatesAsync(IEnumerable<TimeSeriesData> dataPoints);
}