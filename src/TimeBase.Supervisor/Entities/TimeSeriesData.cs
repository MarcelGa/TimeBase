using System;

namespace TimeBase.Supervisor.Entities;

public record TimeSeriesData(
    DateTime Time,
    string Symbol,
    Guid ProviderId,
    string Interval,
    double Open,
    double High,
    double Low,
    double Close,
    double Volume,
    string Metadata,
    string Payload
);
