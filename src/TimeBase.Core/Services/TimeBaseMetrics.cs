using System.Diagnostics.Metrics;

namespace TimeBase.Core.Services;

/// <summary>
/// Centralized metrics for TimeBase operations.
/// Uses OpenTelemetry Meter API for custom business metrics.
/// </summary>
public class TimeBaseMetrics : ITimeBaseMetrics
{
    private readonly Meter _meter;

    // Provider metrics
    private readonly Counter<long> _providerInstallCounter;
    private readonly Counter<long> _providerUninstallCounter;
    private readonly UpDownCounter<int> _activeProvidersGauge;
    private readonly Counter<long> _providerHealthCheckCounter;

    // Data operation metrics
    private readonly Counter<long> _dataQueryCounter;
    private readonly Histogram<double> _dataQueryDuration;
    private readonly Counter<long> _dataPointsRetrieved;
    private readonly Counter<long> _dataPointsStored;

    // Error metrics
    private readonly Counter<long> _operationErrors;

    public TimeBaseMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create("TimeBase.Core");

        // Provider metrics
        _providerInstallCounter = _meter.CreateCounter<long>(
            "timebase.provider.installs",
            description: "Total number of provider installations");

        _providerUninstallCounter = _meter.CreateCounter<long>(
            "timebase.provider.uninstalls",
            description: "Total number of provider uninstalls");

        _activeProvidersGauge = _meter.CreateUpDownCounter<int>(
            "timebase.provider.active",
            description: "Number of currently active providers");

        _providerHealthCheckCounter = _meter.CreateCounter<long>(
            "timebase.provider.health_checks",
            description: "Total number of provider health checks");

        // Data operation metrics
        _dataQueryCounter = _meter.CreateCounter<long>(
            "timebase.data.queries",
            description: "Total number of data queries");

        _dataQueryDuration = _meter.CreateHistogram<double>(
            "timebase.data.query.duration",
            unit: "ms",
            description: "Duration of data queries in milliseconds");

        _dataPointsRetrieved = _meter.CreateCounter<long>(
            "timebase.data.points.retrieved",
            description: "Total number of data points retrieved");

        _dataPointsStored = _meter.CreateCounter<long>(
            "timebase.data.points.stored",
            description: "Total number of data points stored");

        // Error metrics
        _operationErrors = _meter.CreateCounter<long>(
            "timebase.errors",
            description: "Total number of operation errors");
    }

    // Provider operation methods
    public void RecordProviderInstall(string providerSlug, bool success)
    {
        _providerInstallCounter.Add(1,
            new KeyValuePair<string, object?>("provider", providerSlug),
            new KeyValuePair<string, object?>("success", success));
    }

    public void RecordProviderUninstall(string providerSlug, bool success)
    {
        _providerUninstallCounter.Add(1,
            new KeyValuePair<string, object?>("provider", providerSlug),
            new KeyValuePair<string, object?>("success", success));
    }

    public void UpdateActiveProviders(int delta)
    {
        _activeProvidersGauge.Add(delta);
    }

    public void RecordProviderHealth(string providerSlug, bool healthy)
    {
        _providerHealthCheckCounter.Add(1,
            new KeyValuePair<string, object?>("provider", providerSlug),
            new KeyValuePair<string, object?>("healthy", healthy));
    }

    // Data operation methods
    public void RecordDataQuery(string symbol, string interval, int dataPointsCount, double durationMs, bool success)
    {
        _dataQueryCounter.Add(1,
            new KeyValuePair<string, object?>("symbol", symbol),
            new KeyValuePair<string, object?>("interval", interval),
            new KeyValuePair<string, object?>("success", success));

        _dataQueryDuration.Record(durationMs,
            new KeyValuePair<string, object?>("symbol", symbol),
            new KeyValuePair<string, object?>("interval", interval));

        if (success && dataPointsCount > 0)
        {
            _dataPointsRetrieved.Add(dataPointsCount,
                new KeyValuePair<string, object?>("symbol", symbol),
                new KeyValuePair<string, object?>("interval", interval));
        }
    }

    public void RecordDataStore(int dataPointsCount, string? symbol = null, bool success = true)
    {
        if (success && dataPointsCount > 0)
        {
            var tags = symbol != null
                ? new[] { new KeyValuePair<string, object?>("symbol", symbol) }
                : Array.Empty<KeyValuePair<string, object?>>();

            _dataPointsStored.Add(dataPointsCount, tags);
        }
    }

    // Error recording
    public void RecordError(string operation, string errorType)
    {
        _operationErrors.Add(1,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("error_type", errorType));
    }
}