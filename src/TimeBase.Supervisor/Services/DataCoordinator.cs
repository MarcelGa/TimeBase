using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TimeBase.Supervisor.Entities;
using TimeBase.Supervisor.Data;

namespace TimeBase.Supervisor.Services
{
    public class DataCoordinator
    {
        private readonly TimeBaseDbContext _db;
        public DataCoordinator(TimeBaseDbContext db)
        {
            _db = db;
        }

        public Task<IEnumerable<TimeSeriesData>> GetHistoricalAsync(string symbol, string interval, DateTime start, DateTime end)
        {
            // TODO: Implement actual query against hypertable
            return Task.FromResult<IEnumerable<TimeSeriesData>>(new List<TimeSeriesData>());
        }
    }
}
