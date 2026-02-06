using System.Collections.Concurrent;
using System.Threading.Channels;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using TimeBase.Core.Infrastructure.Entities;
using TimeBase.Core.Providers.Services;
using TimeBase.Core.Shared.Services;

namespace TimeBase.Core.Data.Services;

/// <summary>
/// Background service that manages real-time data streaming from providers.
/// It subscribes to providers on behalf of UI clients and broadcasts updates via SignalR.
/// </summary>
public class RealTimeStreamingService : BackgroundService
{
    private readonly ILogger<RealTimeStreamingService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IMarketBroadcaster _broadcaster;
    private readonly ITimeBaseMetrics _metrics;

    /// <summary>
    /// Active subscriptions per provider, keyed by provider slug.
    /// Value is a dictionary of "symbol:interval" -> subscription count.
    /// </summary>
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, int>> _subscriptions = new();

    /// <summary>
    /// Active provider streams, keyed by provider slug.
    /// </summary>
    private readonly ConcurrentDictionary<string, ProviderStreamContext> _providerStreams = new();

    /// <summary>
    /// Channel for receiving subscription requests from SignalR hub.
    /// </summary>
    private readonly Channel<SubscriptionRequest> _subscriptionChannel;

    public RealTimeStreamingService(
        ILogger<RealTimeStreamingService> logger,
        IServiceProvider serviceProvider,
        IMarketBroadcaster broadcaster,
        ITimeBaseMetrics metrics)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _broadcaster = broadcaster;
        _metrics = metrics;
        _subscriptionChannel = Channel.CreateUnbounded<SubscriptionRequest>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });
    }

    /// <summary>
    /// Subscribe to real-time data for a symbol/interval combination.
    /// Called by the SignalR hub when a client subscribes.
    /// </summary>
    public async Task SubscribeAsync(string symbol, string interval = "1m")
    {
        await _subscriptionChannel.Writer.WriteAsync(new SubscriptionRequest(
            Action: SubscriptionAction.Subscribe,
            Symbol: symbol.ToUpperInvariant(),
            Interval: interval));

        _logger.LogInformation("Subscription request queued: {Symbol}/{Interval}", symbol, interval);
    }

    /// <summary>
    /// Unsubscribe from real-time data for a symbol/interval combination.
    /// Called by the SignalR hub when a client unsubscribes.
    /// </summary>
    public async Task UnsubscribeAsync(string symbol, string interval = "1m")
    {
        await _subscriptionChannel.Writer.WriteAsync(new SubscriptionRequest(
            Action: SubscriptionAction.Unsubscribe,
            Symbol: symbol.ToUpperInvariant(),
            Interval: interval));

        _logger.LogInformation("Unsubscription request queued: {Symbol}/{Interval}", symbol, interval);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Real-time streaming service started");

        // Wait a bit for providers to be available
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        // Start processing subscription requests
        var subscriptionProcessor = ProcessSubscriptionRequestsAsync(stoppingToken);

        try
        {
            await subscriptionProcessor;
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }

        // Stop all provider streams
        foreach (var stream in _providerStreams.Values)
        {
            stream.CancellationSource.Cancel();
        }

        _logger.LogInformation("Real-time streaming service stopped");
    }

    /// <summary>
    /// Process subscription requests from the channel.
    /// </summary>
    private async Task ProcessSubscriptionRequestsAsync(CancellationToken cancellationToken)
    {
        await foreach (var request in _subscriptionChannel.Reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                await HandleSubscriptionRequestAsync(request, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling subscription request: {Action} {Symbol}/{Interval}",
                    request.Action, request.Symbol, request.Interval);
            }
        }
    }

    /// <summary>
    /// Handle a subscription request.
    /// </summary>
    private async Task HandleSubscriptionRequestAsync(SubscriptionRequest request, CancellationToken cancellationToken)
    {
        // Get providers that support real-time streaming
        using var scope = _serviceProvider.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<IProviderRegistry>();
        var providers = await registry.GetAllProvidersAsync(enabled: true);

        // Find providers that support real-time streaming by checking their capabilities
        var realtimeProviders = new List<Provider>();
        foreach (var provider in providers)
        {
            var capabilities = registry.GetCachedCapabilities(provider);
            if (capabilities?.SupportsRealtime == true)
            {
                realtimeProviders.Add(provider);
            }
        }

        if (realtimeProviders.Count == 0)
        {
            _logger.LogWarning("No providers support real-time streaming");
            return;
        }

        var subscriptionKey = $"{request.Symbol}:{request.Interval}";

        foreach (var provider in realtimeProviders)
        {
            // Get or create subscription tracking for this provider
            var providerSubs = _subscriptions.GetOrAdd(provider.Slug, _ => new ConcurrentDictionary<string, int>());

            if (request.Action == SubscriptionAction.Subscribe)
            {
                // Increment subscription count
                var count = providerSubs.AddOrUpdate(subscriptionKey, 1, (_, c) => c + 1);

                _logger.LogDebug("Subscription count for {Key} on {Provider}: {Count}",
                    subscriptionKey, provider.Slug, count);

                // If this is the first subscription, start streaming from provider
                if (count == 1)
                {
                    await EnsureProviderStreamAsync(provider, cancellationToken);
                    await SendControlMessageAsync(provider.Slug, new StreamControlMessage(
                        StreamControlAction.Subscribe,
                        request.Symbol,
                        request.Interval));
                }
            }
            else if (request.Action == SubscriptionAction.Unsubscribe)
            {
                // Decrement subscription count
                if (providerSubs.TryGetValue(subscriptionKey, out var currentCount))
                {
                    var newCount = currentCount - 1;
                    if (newCount <= 0)
                    {
                        providerSubs.TryRemove(subscriptionKey, out _);

                        // Send unsubscribe to provider
                        await SendControlMessageAsync(provider.Slug, new StreamControlMessage(
                            StreamControlAction.Unsubscribe,
                            request.Symbol,
                            request.Interval));

                        _logger.LogDebug("Unsubscribed from {Key} on {Provider}",
                            subscriptionKey, provider.Slug);
                    }
                    else
                    {
                        providerSubs[subscriptionKey] = newCount;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Ensure a stream is running for the given provider.
    /// </summary>
    private async Task EnsureProviderStreamAsync(Provider provider, CancellationToken cancellationToken)
    {
        if (_providerStreams.ContainsKey(provider.Slug))
        {
            return; // Stream already running
        }

        _logger.LogInformation("Starting stream for provider {Slug}", provider.Slug);

        var streamContext = new ProviderStreamContext(
            Provider: provider,
            ControlChannel: Channel.CreateUnbounded<StreamControlMessage>(),
            CancellationSource: CancellationTokenSource.CreateLinkedTokenSource(cancellationToken));

        if (!_providerStreams.TryAdd(provider.Slug, streamContext))
        {
            // Another thread already added it
            return;
        }

        // Start streaming in background
        _ = Task.Run(async () =>
        {
            try
            {
                await StreamFromProviderAsync(streamContext);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Provider stream error for {Slug}", provider.Slug);
            }
            finally
            {
                _providerStreams.TryRemove(provider.Slug, out _);
                _logger.LogInformation("Provider stream stopped for {Slug}", provider.Slug);
            }
        }, cancellationToken);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Stream data from a provider and broadcast to SignalR clients.
    /// </summary>
    private async Task StreamFromProviderAsync(ProviderStreamContext context)
    {
        using var scope = _serviceProvider.CreateScope();
        var providerClient = scope.ServiceProvider.GetRequiredService<IProviderClient>();

        try
        {
            await foreach (var data in providerClient.StreamRealTimeDataAsync(
                context.Provider,
                context.ControlChannel.Reader,
                context.CancellationSource.Token))
            {
                // Broadcast to SignalR clients
                await _broadcaster.BroadcastPriceUpdateAsync(data);

                _metrics.RecordDataQuery(data.Symbol, data.Interval, 1, 0, success: true);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on cancellation
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error streaming from provider {Slug}", context.Provider.Slug);
            _metrics.RecordError("realtime_stream", ex.GetType().Name);
        }
    }

    /// <summary>
    /// Send a control message to a provider's stream.
    /// </summary>
    private async Task SendControlMessageAsync(string providerSlug, StreamControlMessage message)
    {
        if (_providerStreams.TryGetValue(providerSlug, out var context))
        {
            await context.ControlChannel.Writer.WriteAsync(message);
        }
    }

    /// <summary>
    /// Context for an active provider stream.
    /// </summary>
    private record ProviderStreamContext(
        Provider Provider,
        Channel<StreamControlMessage> ControlChannel,
        CancellationTokenSource CancellationSource);
}

/// <summary>
/// Subscription request from SignalR hub.
/// </summary>
internal record SubscriptionRequest(
    SubscriptionAction Action,
    string Symbol,
    string Interval);

/// <summary>
/// Subscription action type.
/// </summary>
internal enum SubscriptionAction
{
    Subscribe,
    Unsubscribe
}