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
    /// Get a provider by its ID.
    /// </summary>
    Task<Provider?> GetProviderByIdAsync(Guid id);

    /// <summary>
    /// Uninstall a provider by ID.
    /// </summary>
    Task<bool> UninstallProviderAsync(Guid id);

    /// <summary>
    /// Enable or disable a provider.
    /// </summary>
    Task<Provider?> SetProviderEnabledAsync(Guid id, bool enabled);

    /// <summary>
    /// Update provider capabilities by querying the provider via gRPC.
    /// </summary>
    Task<Provider?> UpdateCapabilitiesAsync(Guid id);

    /// <summary>
    /// Get cached capabilities for a provider from the database.
    /// </summary>
    ProviderCapabilities? GetCachedCapabilities(Provider provider);

    /// <summary>
    /// Update capabilities for all enabled providers.
    /// </summary>
    Task UpdateAllCapabilitiesAsync();
}