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
    Task<Provider> InstallProviderAsync(string repositoryUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all providers, optionally filtered by enabled status.
    /// </summary>
    Task<List<Provider>> GetAllProvidersAsync(bool? enabled = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a provider by its slug.
    /// </summary>
    Task<Provider?> GetProviderBySlugAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uninstall a provider by slug.
    /// </summary>
    Task<bool> UninstallProviderAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enable or disable a provider by slug.
    /// </summary>
    Task<Provider?> SetProviderEnabledAsync(string slug, bool enabled, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update provider capabilities by slug.
    /// </summary>
    Task<Provider?> UpdateCapabilitiesAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get cached capabilities for a provider from the database.
    /// </summary>
    ProviderCapabilities? GetCachedCapabilities(Provider provider);

    /// <summary>
    /// Update capabilities for all enabled providers.
    /// </summary>
    Task UpdateAllCapabilitiesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get symbols for all enabled providers or a specific provider slug.
    /// </summary>
    /// <param name="providerSlug">Optional provider slug to filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary keyed by provider slug</returns>
    Task<Dictionary<string, List<ProviderSymbol>>> GetAllSymbolsAsync(string? providerSlug = null, CancellationToken cancellationToken = default);
}