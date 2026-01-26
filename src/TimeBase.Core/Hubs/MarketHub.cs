using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace TimeBase.Core.Hubs;

/// <summary>
/// SignalR hub for real-time market data updates.
/// Clients can subscribe to specific symbols to receive price updates.
/// </summary>
public class MarketHub : Hub
{
    private readonly ILogger<MarketHub> _logger;

    public MarketHub(ILogger<MarketHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Subscribe to price updates for a specific symbol.
    /// </summary>
    /// <param name="symbol">The symbol to subscribe to (e.g., "AAPL")</param>
    public async Task SubscribeToSymbol(string symbol)
    {
        var normalizedSymbol = symbol.ToUpperInvariant();
        await Groups.AddToGroupAsync(Context.ConnectionId, normalizedSymbol);
        _logger.LogInformation(
            "Client {ConnectionId} subscribed to symbol {Symbol}",
            Context.ConnectionId, normalizedSymbol);
    }

    /// <summary>
    /// Unsubscribe from price updates for a specific symbol.
    /// </summary>
    /// <param name="symbol">The symbol to unsubscribe from</param>
    public async Task UnsubscribeFromSymbol(string symbol)
    {
        var normalizedSymbol = symbol.ToUpperInvariant();
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, normalizedSymbol);
        _logger.LogInformation(
            "Client {ConnectionId} unsubscribed from symbol {Symbol}",
            Context.ConnectionId, normalizedSymbol);
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
