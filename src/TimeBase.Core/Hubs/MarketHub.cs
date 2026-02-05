using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

using TimeBase.Core.Services;

namespace TimeBase.Core.Hubs;

/// <summary>
/// SignalR hub for real-time market data updates.
/// Clients can subscribe to specific symbols to receive price updates.
/// </summary>
public class MarketHub : Hub
{
    private readonly ILogger<MarketHub> _logger;
    private readonly RealTimeStreamingService _streamingService;

    public MarketHub(
        ILogger<MarketHub> logger,
        RealTimeStreamingService streamingService)
    {
        _logger = logger;
        _streamingService = streamingService;
    }

    /// <summary>
    /// Subscribe to price updates for a specific symbol.
    /// </summary>
    /// <param name="symbol">The symbol to subscribe to (e.g., "AAPL")</param>
    /// <param name="interval">The interval to subscribe to (e.g., "1m", default)</param>
    public async Task SubscribeToSymbol(string symbol, string interval = "1m")
    {
        var normalizedSymbol = symbol.ToUpperInvariant();

        // Add to SignalR group for broadcasting
        await Groups.AddToGroupAsync(Context.ConnectionId, normalizedSymbol);

        // Subscribe to real-time data from providers
        await _streamingService.SubscribeAsync(normalizedSymbol, interval);

        _logger.LogInformation(
            "Client {ConnectionId} subscribed to symbol {Symbol}/{Interval}",
            Context.ConnectionId, normalizedSymbol, interval);
    }

    /// <summary>
    /// Unsubscribe from price updates for a specific symbol.
    /// </summary>
    /// <param name="symbol">The symbol to unsubscribe from</param>
    /// <param name="interval">The interval to unsubscribe from (e.g., "1m", default)</param>
    public async Task UnsubscribeFromSymbol(string symbol, string interval = "1m")
    {
        var normalizedSymbol = symbol.ToUpperInvariant();

        // Remove from SignalR group
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, normalizedSymbol);

        // Unsubscribe from real-time data from providers
        await _streamingService.UnsubscribeAsync(normalizedSymbol, interval);

        _logger.LogInformation(
            "Client {ConnectionId} unsubscribed from symbol {Symbol}/{Interval}",
            Context.ConnectionId, normalizedSymbol, interval);
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation(
            "Client disconnected: {ConnectionId}. Reason: {Exception}",
            Context.ConnectionId, exception?.Message ?? "Normal disconnect");
        await base.OnDisconnectedAsync(exception);
    }
}