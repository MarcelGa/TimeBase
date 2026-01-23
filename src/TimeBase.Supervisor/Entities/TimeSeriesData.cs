using System;

namespace TimeBase.Supervisor.Entities
{
    public class TimeSeriesData
    {
        public DateTime Time { get; set; }
        public string Symbol { get; set; }
        public Guid ProviderId { get; set; }
        public string Interval { get; set; }
        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Close { get; set; }
        public double Volume { get; set; }
        public string Metadata { get; set; }
        public string Payload { get; set; }
    }
}
