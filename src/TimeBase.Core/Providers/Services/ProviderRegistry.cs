using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using TimeBase.Core.Infrastructure.Data;
using TimeBase.Core.Infrastructure.Entities;
using TimeBase.Core.Shared.Services;

namespace TimeBase.Core.Providers.Services;

public class ProviderRegistry(
    TimeBaseDbContext db,
    ILogger<ProviderRegistry> logger,
    ITimeBaseMetrics metrics,
    IProviderClient providerClient) : IProviderRegistry
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
                GrpcEndpoint: $"timebase-{slug}:50051",  // Default Docker Compose endpoint
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
    /// Uninstall a provider by slug.
    /// </summary>
    public async Task<bool> UninstallProviderAsync(string slug)
    {
        var provider = await GetProviderBySlugAsync(slug);
        if (provider == null)
        {
            return false;
        }

        return await UninstallProviderAsync(provider.Id);
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

    /// <summary>
    /// Enable or disable a provider by slug.
    /// </summary>
    public async Task<Provider?> SetProviderEnabledAsync(string slug, bool enabled)
    {
        var provider = await GetProviderBySlugAsync(slug);
        if (provider == null)
        {
            return null;
        }

        return await SetProviderEnabledAsync(provider.Id, enabled);
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

    /// <summary>
    /// Update provider capabilities by querying the provider via gRPC.
    /// Caches the result in the database for faster lookups.
    /// </summary>
    public async Task<Provider?> UpdateCapabilitiesAsync(Guid id)
    {
        var provider = await db.Providers.FindAsync(id);
        if (provider == null)
        {
            logger.LogWarning("Provider {ProviderId} not found", id);
            return null;
        }

        logger.LogInformation("Updating capabilities for provider {Slug}", provider.Slug);

        try
        {
            var capabilities = await providerClient.GetCapabilitiesAsync(provider);
            if (capabilities == null)
            {
                logger.LogWarning("Failed to fetch capabilities from provider {Slug}", provider.Slug);
                return provider;
            }

            var capabilitiesJson = JsonSerializer.Serialize(capabilities);
            var updated = provider with
            {
                Capabilities = capabilitiesJson,
                Version = capabilities.Version, // Update version from provider
                Name = capabilities.Name, // Update name from provider
                UpdatedAt = DateTime.UtcNow
            };

            db.Entry(provider).CurrentValues.SetValues(updated);
            await db.SaveChangesAsync();

            logger.LogInformation(
                "Updated capabilities for provider {Slug}: Historical={Historical}, Realtime={Realtime}, DataTypes={DataTypes}",
                provider.Slug,
                capabilities.SupportsHistorical,
                capabilities.SupportsRealtime,
                string.Join(", ", capabilities.DataTypes));

            return updated;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update capabilities for provider {Slug}", provider.Slug);
            metrics.RecordError("provider_capabilities_update", ex.GetType().Name);
            throw;
        }
    }

    /// <summary>
    /// Update provider capabilities by slug.
    /// </summary>
    public async Task<Provider?> UpdateCapabilitiesAsync(string slug)
    {
        var provider = await GetProviderBySlugAsync(slug);
        if (provider == null)
        {
            return null;
        }

        return await UpdateCapabilitiesAsync(provider.Id);
    }

    /// <summary>
    /// Get cached capabilities for a provider from the database.
    /// Returns null if capabilities haven't been cached yet.
    /// </summary>
    public ProviderCapabilities? GetCachedCapabilities(Provider provider)
    {
        if (string.IsNullOrWhiteSpace(provider.Capabilities))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ProviderCapabilities>(provider.Capabilities);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to deserialize capabilities for provider {Slug}", provider.Slug);
            return null;
        }
    }

    /// <summary>
    /// Update capabilities for all enabled providers.
    /// </summary>
    public async Task UpdateAllCapabilitiesAsync()
    {
        logger.LogInformation("Updating capabilities for all enabled providers");

        var enabledProviders = await GetAllProvidersAsync(enabled: true);

        // Process sequentially to avoid DbContext concurrency issues
        foreach (var provider in enabledProviders)
        {
            try
            {
                await UpdateCapabilitiesAsync(provider.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to update capabilities for provider {Slug}", provider.Slug);
                // Continue with other providers even if one fails
            }
        }

        logger.LogInformation("Updated capabilities for {Count} providers", enabledProviders.Count);
    }

    /// <summary>
    /// Get symbols for all enabled providers or a specific provider slug.
    /// </summary>
    public async Task<Dictionary<string, List<ProviderSymbol>>> GetAllSymbolsAsync(string? providerSlug = null)
    {
        var query = db.Providers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(providerSlug))
        {
            query = query.Where(provider => provider.Slug == providerSlug);
        }

        var providers = await query.Where(provider => provider.Enabled)
            .OrderBy(provider => provider.CreatedAt)
            .ToListAsync();

        var results = new Dictionary<string, List<ProviderSymbol>>();

        foreach (var provider in providers)
        {
            try
            {
                var symbols = await providerClient.GetSymbolsAsync(provider);
                if (symbols == null)
                {
                    logger.LogWarning("Failed to fetch symbols from provider {Slug}", provider.Slug);
                    continue;
                }

                results[provider.Slug] = symbols;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to fetch symbols from provider {Slug}", provider.Slug);
                metrics.RecordError("provider_symbols", ex.GetType().Name);
            }
        }

        return results;
    }
}