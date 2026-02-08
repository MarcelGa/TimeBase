using Microsoft.AspNetCore.SignalR;

using TimeBase.Core.Data.Hubs;
using TimeBase.Core.Infrastructure.Entities;

namespace TimeBase.Core.Data.Services;

public class MarketBroadcaster(
    IHubContext<MarketHub> hubContext,
    ILogger<MarketBroadcaster> logger) : IMarketBroadcaster
{
    public async Task BroadcastPriceUpdateAsync(TimeSeriesData data)
    {
        try
        {
            var symbol = data.Symbol.ToUpperInvariant();
            await hubContext.Clients.Group(symbol).SendAsync("ReceivePriceUpdate", data);

            logger.LogDebug(
                "Broadcasted price update for {Symbol}: O={Open}, H={High}, L={Low}, C={Close}",
                symbol, data.Open, data.High, data.Low, data.Close);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to broadcast price update for {Symbol}", data.Symbol);
        }
    }

    public async Task BroadcastPriceUpdatesAsync(IEnumerable<TimeSeriesData> dataPoints)
    {
        // Group by symbol to send only the latest data point per symbol
        foreach (var group in dataPoints.GroupBy(d => d.Symbol.ToUpperInvariant()))
        {
            try
            {
                // Send the latest data point for each symbol
                var latestData = group.OrderByDescending(d => d.Time).First();
                await hubContext.Clients.Group(group.Key).SendAsync("ReceivePriceUpdate", latestData);

                logger.LogDebug(
                    "Broadcasted price update for {Symbol}: C={Close}",
                    group.Key, latestData.Close);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to broadcast price update for {Symbol}", group.Key);
            }
        }
    }
}