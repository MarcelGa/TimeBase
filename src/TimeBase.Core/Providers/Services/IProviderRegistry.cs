using TimeBase.Core.Infrastructure.Entities;

namespace TimeBase.Core.Providers.Services;

/// <summary>
/// Interface for managing provider lifecycle and capabilities.
/// </summary>
public interface IProviderRegistry
{
    /// <summary>
    /// Install a provider from a repository URL.
    /// </summary>
    Task<Provider> InstallProviderAsync(string repositoryUrl);

    /// <summary>
    /// Get all providers, optionally filtered by enabled status.
    /// </summary>
    Task<List<Provider>> GetAllProvidersAsync(bool? enabled = null);

    /// <summary>
    /// Get a provider by its slug.
    /// </summary>
    Task<Provider?> GetProviderBySlugAsync(string slug);

    /// <summary>
    /// Uninstall a provider by slug.
    /// </summary>
    Task<bool> UninstallProviderAsync(string slug);

    /// <summary>
    /// Enable or disable a provider by slug.
    /// </summary>
    Task<Provider?> SetProviderEnabledAsync(string slug, bool enabled);

    /// <summary>
    /// Update provider capabilities by slug.
    /// </summary>
    Task<Provider?> UpdateCapabilitiesAsync(string slug);

    /// <summary>
    /// Get cached capabilities for a provider from the database.
    /// </summary>
    ProviderCapabilities? GetCachedCapabilities(Provider provider);

    /// <summary>
    /// Update capabilities for all enabled providers.
    /// </summary>
    Task UpdateAllCapabilitiesAsync();

    /// <summary>
    /// Get symbols for all enabled providers or a specific provider slug.
    /// </summary>
    /// <param name="providerSlug">Optional provider slug to filter</param>
    /// <returns>Dictionary keyed by provider slug</returns>
    Task<Dictionary<string, List<ProviderSymbol>>> GetAllSymbolsAsync(string? providerSlug = null);
}