using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using TimeBase.Core.Hubs;
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

public class MarketBroadcaster : IMarketBroadcaster
{
    private readonly IHubContext<MarketHub> _hubContext;
    private readonly ILogger<MarketBroadcaster> _logger;

    public MarketBroadcaster(
        IHubContext<MarketHub> hubContext,
        ILogger<MarketBroadcaster> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task BroadcastPriceUpdateAsync(TimeSeriesData data)
    {
        try
        {
            var symbol = data.Symbol.ToUpperInvariant();
            await _hubContext.Clients.Group(symbol).SendAsync("ReceivePriceUpdate", data);
            
            _logger.LogDebug(
                "Broadcasted price update for {Symbol}: O={Open}, H={High}, L={Low}, C={Close}",
                symbol, data.Open, data.High, data.Low, data.Close);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast price update for {Symbol}", data.Symbol);
        }
    }

    public async Task BroadcastPriceUpdatesAsync(IEnumerable<TimeSeriesData> dataPoints)
    {
        // Group by symbol for efficient broadcasting
        var groupedData = dataPoints.GroupBy(d => d.Symbol.ToUpperInvariant());

        foreach (var group in groupedData)
        {
            try
            {
                // Send the latest data point for each symbol
                var latestData = group.OrderByDescending(d => d.Time).First();
                await _hubContext.Clients.Group(group.Key).SendAsync("ReceivePriceUpdate", latestData);
                
                _logger.LogDebug(
                    "Broadcasted price update for {Symbol}: C={Close}",
                    group.Key, latestData.Close);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to broadcast price update for {Symbol}", group.Key);
            }
        }
    }
}
