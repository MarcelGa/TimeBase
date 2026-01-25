using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using TimeBase.Core.Infrastructure.Entities;
using Timebase.Provider;

namespace TimeBase.Core.Services;

/// <summary>
/// gRPC client for communicating with data providers.
/// </summary>
public class ProviderClient(
    ILogger<ProviderClient> logger,
    ITimeBaseMetrics metrics) : IProviderClient
{
    private readonly Dictionary<string, GrpcChannel> _channels = new();

    /// <summary>
    /// Fetch historical time series data from a provider via gRPC.
    /// </summary>
    public async Task<List<Infrastructure.Entities.TimeSeriesData>> GetHistoricalDataAsync(
        Infrastructure.Entities.Provider provider,
        string symbol,
        string interval,
        DateTime start,
        DateTime end)
    {
        if (string.IsNullOrEmpty(provider.GrpcEndpoint))
        {
            logger.LogWarning("Provider {Slug} has no gRPC endpoint configured", provider.Slug);
            return new List<Infrastructure.Entities.TimeSeriesData>();
        }

        logger.LogInformation(
            "Fetching historical data for {Symbol} from provider {Provider} ({Endpoint})",
            symbol, provider.Slug, provider.GrpcEndpoint);

        var startTime = DateTime.UtcNow;
        try
        {
            var channel = GetOrCreateChannel(provider.GrpcEndpoint);
            var client = new DataProvider.DataProviderClient(channel);

            var request = new HistoricalDataRequest
            {
                Symbol = symbol,
                Interval = interval,
                StartTime = Timestamp.FromDateTime(DateTime.SpecifyKind(start, DateTimeKind.Utc)),
                EndTime = Timestamp.FromDateTime(DateTime.SpecifyKind(end, DateTimeKind.Utc))
            };

            var result = new List<Infrastructure.Entities.TimeSeriesData>();
            
            using var call = client.GetHistoricalData(request);

            await foreach (var dataPoint in call.ResponseStream.ReadAllAsync())
            {
                result.Add(new Infrastructure.Entities.TimeSeriesData(
                    Time: dataPoint.Timestamp.ToDateTime(),
                    Symbol: dataPoint.Symbol,
                    ProviderId: provider.Id,
                    Interval: dataPoint.Interval,
                    Open: dataPoint.Open,
                    High: dataPoint.High,
                    Low: dataPoint.Low,
                    Close: dataPoint.Close,
                    Volume: dataPoint.Volume,
                    Metadata: dataPoint.Metadata.Count > 0 
                        ? System.Text.Json.JsonSerializer.Serialize(dataPoint.Metadata) 
                        : null
                ));
            }

            var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
            metrics.RecordDataQuery(symbol, interval, result.Count, duration, success: true);

            logger.LogInformation(
                "Fetched {Count} data points for {Symbol} from provider {Provider}",
                result.Count, symbol, provider.Slug);

            return result;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
            metrics.RecordDataQuery(symbol, interval, 0, duration, success: false);
            logger.LogError(ex, 
                "Provider {Provider} is unavailable at {Endpoint}", 
                provider.Slug, provider.GrpcEndpoint);
            return new List<Infrastructure.Entities.TimeSeriesData>();
        }
        catch (Exception ex)
        {
            var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
            metrics.RecordDataQuery(symbol, interval, 0, duration, success: false);
            metrics.RecordError("provider_grpc_call", ex.GetType().Name);
            logger.LogError(ex, 
                "Failed to fetch data from provider {Provider}", 
                provider.Slug);
            throw;
        }
    }

    /// <summary>
    /// Get provider capabilities via gRPC.
    /// </summary>
    public async Task<ProviderCapabilities?> GetCapabilitiesAsync(Provider provider)
    {
        if (string.IsNullOrEmpty(provider.GrpcEndpoint))
        {
            logger.LogWarning("Provider {Slug} has no gRPC endpoint configured", provider.Slug);
            return null;
        }

        try
        {
            var channel = GetOrCreateChannel(provider.GrpcEndpoint);
            var client = new DataProvider.DataProviderClient(channel);

            var response = await client.GetCapabilitiesAsync(new Empty());

            return new ProviderCapabilities(
                Name: response.Name,
                Version: response.Version,
                Slug: response.Slug,
                SupportsHistorical: response.SupportsHistorical,
                SupportsRealtime: response.SupportsRealtime,
                DataTypes: response.DataTypes.ToList(),
                Intervals: response.Intervals.ToList()
            );
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            logger.LogWarning(ex, 
                "Provider {Provider} is unavailable at {Endpoint}", 
                provider.Slug, provider.GrpcEndpoint);
            return null;
        }
        catch (Exception ex)
        {
            metrics.RecordError("provider_capabilities", ex.GetType().Name);
            logger.LogError(ex, 
                "Failed to get capabilities from provider {Provider}", 
                provider.Slug);
            return null;
        }
    }

    /// <summary>
    /// Check if provider is healthy via gRPC.
    /// </summary>
    public async Task<bool> IsHealthyAsync(Provider provider)
    {
        if (string.IsNullOrEmpty(provider.GrpcEndpoint))
        {
            return false;
        }

        try
        {
            var channel = GetOrCreateChannel(provider.GrpcEndpoint);
            var client = new DataProvider.DataProviderClient(channel);

            var response = await client.HealthCheckAsync(new Empty(), 
                deadline: DateTime.UtcNow.AddSeconds(5));

            return response.Status == HealthStatus.Types.Status.Healthy;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get or create a gRPC channel for the given endpoint.
    /// Channels are cached for reuse.
    /// </summary>
    private GrpcChannel GetOrCreateChannel(string endpoint)
    {
        if (_channels.TryGetValue(endpoint, out var existingChannel))
        {
            return existingChannel;
        }

        // Create channel with HTTP/2 over plain text (for internal Docker network)
        var channel = GrpcChannel.ForAddress($"http://{endpoint}", new GrpcChannelOptions
        {
            HttpHandler = new SocketsHttpHandler
            {
                PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
                KeepAlivePingDelay = TimeSpan.FromSeconds(60),
                KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
                EnableMultipleHttp2Connections = true
            }
        });

        _channels[endpoint] = channel;
        logger.LogInformation("Created gRPC channel to {Endpoint}", endpoint);

        return channel;
    }
}
