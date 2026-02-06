using TimeBase.Core.Infrastructure.Entities;

namespace TimeBase.Core.Data.Models;

// Data endpoints responses
public record GetHistoricalDataResponse(
    string Symbol,
    string Interval,
    DateTime Start,
    DateTime End,
    int Count,
    List<TimeSeriesData> Data
);

public record GetDataSummaryResponse(
    DataSummary Summary
);

public record GetProvidersForSymbolResponse(
    string Symbol,
    int Count,
    List<Provider> Providers
);