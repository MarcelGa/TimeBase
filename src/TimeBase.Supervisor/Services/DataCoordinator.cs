using TimeBase.Supervisor.Entities;
using TimeBase.Supervisor.Data;

namespace TimeBase.Supervisor.Services
{
    public class DataCoordinator(TimeBaseDbContext db)
    {
        private readonly TimeBaseDbContext _db = db;

        public Task<IEnumerable<TimeSeriesData>> GetHistoricalAsync(string symbol, string interval, DateTime start, DateTime end)
        {
            // TODO: Implement actual query against hypertable
            return Task.FromResult<IEnumerable<TimeSeriesData>>(new List<TimeSeriesData>());
        }
    }
}
