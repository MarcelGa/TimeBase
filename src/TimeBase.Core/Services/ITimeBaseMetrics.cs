namespace TimeBase.Core.Services;

/// <summary>
/// Interface for TimeBase business metrics tracking.
/// </summary>
public interface ITimeBaseMetrics
{
    /// <summary>
    /// Record a provider installation attempt.
    /// </summary>
    void RecordProviderInstall(string providerSlug, bool success);

    /// <summary>
    /// Record a provider uninstallation attempt.
    /// </summary>
    void RecordProviderUninstall(string providerSlug, bool success);

    /// <summary>
    /// Update the count of active providers.
    /// </summary>
    void UpdateActiveProviders(int delta);

    /// <summary>
    /// Record a data query operation.
    /// </summary>
    void RecordDataQuery(string symbol, string interval, int dataPointsCount, double durationMs, bool success);

    /// <summary>
    /// Record data storage operation.
    /// </summary>
    void RecordDataStore(int dataPointsCount, string? symbol = null, bool success = true);

    /// <summary>
    /// Record an error.
    /// </summary>
    void RecordError(string operation, string errorType);
}
