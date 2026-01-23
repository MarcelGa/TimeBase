using TimeBase.Supervisor.Data;
using TimeBase.Supervisor.Entities;

namespace TimeBase.Supervisor.Services;

public class ProviderRegistry(TimeBaseDbContext db)
{
    public async Task InstallProviderAsync(string repositoryUrl)
    {
        // Minimal placeholder: store a provider entry with slug derived from URL
        var slug = "provider-" + Guid.NewGuid().ToString("n").Substring(0, 8);
        var provider = new Provider(
            Id: Guid.NewGuid(),
            Slug: slug,
            Name: System.IO.Path.GetFileName(repositoryUrl) ?? slug,
            Version: "0.1.0",
            RepositoryUrl: repositoryUrl,
            ImageUrl: null,
            Enabled: true,
            Config: null,
            Capabilities: null,
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow);
        db.Providers.Add(provider);
        await db.SaveChangesAsync();
    }

    public IQueryable<Provider> GetAllProviders()
    {
        return db.Providers.AsQueryable();
    }
}