using System;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using TimeBase.Supervisor.Data;
using TimeBase.Supervisor.Entities;

namespace TimeBase.Supervisor.Services
{
    public class ProviderRegistry
    {
        private readonly TimeBaseDbContext _db;
        public ProviderRegistry(TimeBaseDbContext db)
        {
            _db = db;
        }

        public async Task InstallProviderAsync(string repositoryUrl)
        {
            // Minimal placeholder: store a provider entry with slug derived from URL
            var slug = "provider-" + Guid.NewGuid().ToString("n").Substring(0, 8);
            var provider = new Provider
            {
                Id = Guid.NewGuid(),
                Slug = slug,
                Name = System.IO.Path.GetFileName(repositoryUrl) ?? slug,
                Version = "0.1.0",
                RepositoryUrl = repositoryUrl,
                ImageUrl = null,
                Enabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.Providers.Add(provider);
            await _db.SaveChangesAsync();
        }

        public IQueryable<Provider> GetAllProviders()
        {
            return _db.Providers.AsQueryable();
        }
    }
}
