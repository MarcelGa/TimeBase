using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TimeBase.Core.Infrastructure.Data;

public class TimeBaseDbContextFactory : IDesignTimeDbContextFactory<TimeBaseDbContext>
{
    public TimeBaseDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TimeBaseDbContext>();

        // Use a default connection string for migrations
        // This will be overridden at runtime with the actual configuration
        optionsBuilder.UseNpgsql("Host=localhost;Database=timebase;Username=timebase;Password=timebase_dev");

        return new TimeBaseDbContext(optionsBuilder.Options);
    }
}