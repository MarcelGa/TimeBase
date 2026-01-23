using Microsoft.EntityFrameworkCore;
using TimeBase.Core.Entities;

namespace TimeBase.Core.Data;

public class TimeBaseDbContext(DbContextOptions<TimeBaseDbContext> options) : DbContext(options)
{
    public DbSet<Provider> Providers { get; set; }
    public DbSet<Symbol> Symbols { get; set; }
    public DbSet<TimeSeriesData> TimeSeries { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Provider configuration
        modelBuilder.Entity<Provider>(entity =>
        {
            entity.ToTable("providers");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Slug).HasColumnName("slug").IsRequired().HasMaxLength(100);
            entity.Property(e => e.Name).HasColumnName("name").IsRequired().HasMaxLength(200);
            entity.Property(e => e.Version).HasColumnName("version").IsRequired().HasMaxLength(50);
            entity.Property(e => e.Enabled).HasColumnName("enabled");
            entity.Property(e => e.RepositoryUrl).HasColumnName("repository_url").IsRequired().HasMaxLength(500);
            entity.Property(e => e.ImageUrl).HasColumnName("image_url").HasMaxLength(500);
            entity.Property(e => e.Config).HasColumnName("config").HasColumnType("jsonb");
            entity.Property(e => e.Capabilities).HasColumnName("capabilities").HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            
            entity.HasIndex(e => e.Slug).IsUnique();
        });

        // Symbol configuration
        modelBuilder.Entity<Symbol>(entity =>
        {
            entity.ToTable("symbols");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.SymbolValue).HasColumnName("symbol").IsRequired().HasMaxLength(50);
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(200);
            entity.Property(e => e.Type).HasColumnName("type").HasMaxLength(50);
            entity.Property(e => e.Exchange).HasColumnName("exchange").HasMaxLength(50);
            entity.Property(e => e.Currency).HasColumnName("currency").HasMaxLength(10);
            entity.Property(e => e.Metadata).HasColumnName("metadata").HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            
            entity.HasIndex(e => e.SymbolValue).IsUnique();
        });

        // TimeSeriesData configuration - maps to existing hypertable
        modelBuilder.Entity<TimeSeriesData>(entity =>
        {
            entity.ToTable("time_series_data");
            entity.HasNoKey(); // Hypertable doesn't have a traditional primary key
            entity.Property(e => e.Time).HasColumnName("time");
            entity.Property(e => e.Symbol).HasColumnName("symbol").IsRequired().HasMaxLength(50);
            entity.Property(e => e.ProviderId).HasColumnName("provider_id");
            entity.Property(e => e.Interval).HasColumnName("interval").IsRequired().HasMaxLength(10);
            entity.Property(e => e.Open).HasColumnName("open");
            entity.Property(e => e.High).HasColumnName("high");
            entity.Property(e => e.Low).HasColumnName("low");
            entity.Property(e => e.Close).HasColumnName("close");
            entity.Property(e => e.Volume).HasColumnName("volume");
            entity.Property(e => e.Metadata).HasColumnName("metadata").HasColumnType("jsonb");
            entity.Property(e => e.Payload).HasColumnName("payload").HasColumnType("jsonb");
        });
    }
}
