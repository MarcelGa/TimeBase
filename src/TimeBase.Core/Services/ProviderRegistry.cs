using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TimeBase.Core.Infrastructure.Data;
using TimeBase.Core.Infrastructure.Entities;

namespace TimeBase.Core.Services;

public class ProviderRegistry(
    TimeBaseDbContext db, 
    ILogger<ProviderRegistry> logger,
    ITimeBaseMetrics metrics)
{
    /// <summary>
    /// Install a provider from a repository URL.
    /// In MVP, this creates a basic provider entry.
    /// Future: Clone repo, parse manifest, build Docker image.
    /// </summary>
    public async Task<Provider> InstallProviderAsync(string repositoryUrl)
    {
        logger.LogInformation("Installing provider from {RepositoryUrl}", repositoryUrl);
        
        // Extract a reasonable slug from the repository URL
        var slug = ExtractSlugFromUrl(repositoryUrl);
        
        try
        {
            // Check if provider already exists
            var existing = await db.Providers.FirstOrDefaultAsync(p => p.RepositoryUrl == repositoryUrl);
            if (existing != null)
            {
                logger.LogWarning("Provider from {RepositoryUrl} already exists with slug {Slug}", repositoryUrl, existing.Slug);
                metrics.RecordProviderInstall(slug, success: true);
                return existing;
            }
            
            var provider = new Provider(
                Id: Guid.NewGuid(),
                Slug: slug,
                Name: ExtractNameFromUrl(repositoryUrl),
                Version: "0.1.0",
                RepositoryUrl: repositoryUrl,
                ImageUrl: null,
                Enabled: true,
                Config: null,
                Capabilities: null,
                CreatedAt: DateTime.UtcNow,
                UpdatedAt: DateTime.UtcNow
            );
            
            db.Providers.Add(provider);
            await db.SaveChangesAsync();
            
            metrics.RecordProviderInstall(slug, success: true);
            metrics.UpdateActiveProviders(1);
            
            logger.LogInformation("Provider {Slug} installed successfully with ID {ProviderId}", slug, provider.Id);
            return provider;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to install provider from {RepositoryUrl}", repositoryUrl);
            metrics.RecordProviderInstall(slug, success: false);
            metrics.RecordError("provider_install", ex.GetType().Name);
            throw;
        }
    }

    /// <summary>
    /// Get all providers, optionally filtered by enabled status.
    /// </summary>
    public async Task<List<Provider>> GetAllProvidersAsync(bool? enabled = null)
    {
        var query = db.Providers.AsQueryable();
        
        if (enabled.HasValue)
        {
            query = query.Where(p => p.Enabled == enabled.Value);
        }
        
        return await query.OrderBy(p => p.CreatedAt).ToListAsync();
    }

    /// <summary>
    /// Get a provider by its slug.
    /// </summary>
    public async Task<Provider?> GetProviderBySlugAsync(string slug)
    {
        return await db.Providers.FirstOrDefaultAsync(p => p.Slug == slug);
    }

    /// <summary>
    /// Get a provider by its ID.
    /// </summary>
    public async Task<Provider?> GetProviderByIdAsync(Guid id)
    {
        return await db.Providers.FindAsync(id);
    }

    /// <summary>
    /// Uninstall a provider by ID.
    /// In MVP, this just removes the database entry.
    /// Future: Stop container, remove image, cleanup volumes.
    /// </summary>
    public async Task<bool> UninstallProviderAsync(Guid id)
    {
        logger.LogInformation("Uninstalling provider {ProviderId}", id);
        
        var provider = await db.Providers.FindAsync(id);
        if (provider == null)
        {
            logger.LogWarning("Provider {ProviderId} not found for uninstall", id);
            return false;
        }
        
        try
        {
            db.Providers.Remove(provider);
            await db.SaveChangesAsync();
            
            metrics.RecordProviderUninstall(provider.Slug, success: true);
            if (provider.Enabled)
            {
                metrics.UpdateActiveProviders(-1);
            }
            
            logger.LogInformation("Provider {Slug} uninstalled successfully", provider.Slug);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to uninstall provider {Slug}", provider.Slug);
            metrics.RecordProviderUninstall(provider.Slug, success: false);
            metrics.RecordError("provider_uninstall", ex.GetType().Name);
            throw;
        }
    }

    /// <summary>
    /// Enable or disable a provider.
    /// </summary>
    public async Task<Provider?> SetProviderEnabledAsync(Guid id, bool enabled)
    {
        var provider = await db.Providers.FindAsync(id);
        if (provider == null)
        {
            logger.LogWarning("Provider {ProviderId} not found", id);
            return null;
        }
        
        // Track change in active providers
        if (provider.Enabled != enabled)
        {
            metrics.UpdateActiveProviders(enabled ? 1 : -1);
        }
        
        var updated = provider with 
        { 
            Enabled = enabled, 
            UpdatedAt = DateTime.UtcNow 
        };
        
        db.Entry(provider).CurrentValues.SetValues(updated);
        await db.SaveChangesAsync();
        
        logger.LogInformation("Provider {Slug} {Status}", provider.Slug, enabled ? "enabled" : "disabled");
        return updated;
    }

    private static string ExtractSlugFromUrl(string url)
    {
        // Extract slug from URL like https://github.com/user/timebase-provider-yahoo
        // Result: yahoo or timebase-provider-yahoo
        var parts = url.TrimEnd('/').Split('/');
        var repoName = parts[^1].Replace(".git", "");
        
        // Try to extract provider name from repo name
        if (repoName.StartsWith("timebase-provider-", StringComparison.OrdinalIgnoreCase))
        {
            return repoName["timebase-provider-".Length..].ToLowerInvariant();
        }
        
        // Fallback: use full repo name as slug
        return repoName.ToLowerInvariant();
    }

    private static string ExtractNameFromUrl(string url)
    {
        var slug = ExtractSlugFromUrl(url);
        
        // Convert slug to a friendly name: yahoo -> Yahoo Finance Provider
        return $"{char.ToUpper(slug[0])}{slug[1..]} Provider";
    }
}
