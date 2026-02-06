namespace TimeBase.Core.Models;

/// <summary>
/// Summary information about available data for a symbol.
/// </summary>
public record DataSummary(
    string Symbol,
    int TotalDataPoints,
    DateTime EarliestDate,
    DateTime LatestDate,
    int Providers,
    List<string> Intervals
);