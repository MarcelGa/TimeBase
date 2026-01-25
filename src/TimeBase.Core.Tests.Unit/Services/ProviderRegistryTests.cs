using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TimeBase.Core.Infrastructure.Data;
using TimeBase.Core.Infrastructure.Entities;
using TimeBase.Core.Services;
using Xunit;

namespace TimeBase.Core.Tests.Services;

public class ProviderRegistryTests : IDisposable
{
    private readonly TimeBaseDbContext _context;
    private readonly Mock<ILogger<ProviderRegistry>> _loggerMock;
    private readonly Mock<ITimeBaseMetrics> _metricsMock;
    private readonly Mock<IProviderClient> _providerClientMock;
    private readonly ProviderRegistry _sut; // System Under Test

    public ProviderRegistryTests()
    {
        // Create in-memory database
        var options = new DbContextOptionsBuilder<TimeBaseDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new TimeBaseDbContext(options);
        _loggerMock = new Mock<ILogger<ProviderRegistry>>();
        _metricsMock = new Mock<ITimeBaseMetrics>();
        _providerClientMock = new Mock<IProviderClient>();

        _sut = new ProviderRegistry(_context, _loggerMock.Object, _metricsMock.Object, _providerClientMock.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task GetAllProvidersAsync_WhenNoProviders_ReturnsEmptyList()
    {
        // Act
        var result = await _sut.GetAllProvidersAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllProvidersAsync_WhenProvidersExist_ReturnsAllProviders()
    {
        // Arrange
        var provider1 = CreateTestProvider("provider-1", "Provider 1");
        var provider2 = CreateTestProvider("provider-2", "Provider 2");
        _context.Providers.AddRange(provider1, provider2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetAllProvidersAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(p => p.Slug == "provider-1");
        result.Should().Contain(p => p.Slug == "provider-2");
    }

    [Fact]
    public async Task GetAllProvidersAsync_WhenFilteringByEnabled_ReturnsOnlyEnabledProviders()
    {
        // Arrange
        var enabledProvider = CreateTestProvider("enabled-provider", "Enabled Provider", enabled: true);
        var disabledProvider = CreateTestProvider("disabled-provider", "Disabled Provider", enabled: false);
        _context.Providers.AddRange(enabledProvider, disabledProvider);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetAllProvidersAsync(enabled: true);

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain(p => p.Slug == "enabled-provider");
    }

    [Fact]
    public async Task GetProviderByIdAsync_WhenProviderExists_ReturnsProvider()
    {
        // Arrange
        var provider = CreateTestProvider("test-provider", "Test Provider");
        _context.Providers.Add(provider);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetProviderByIdAsync(provider.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(provider.Id);
        result.Slug.Should().Be("test-provider");
    }

    [Fact]
    public async Task GetProviderByIdAsync_WhenProviderDoesNotExist_ReturnsNull()
    {
        // Act
        var result = await _sut.GetProviderByIdAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetProviderBySlugAsync_WhenProviderExists_ReturnsProvider()
    {
        // Arrange
        var provider = CreateTestProvider("test-slug", "Test Provider");
        _context.Providers.Add(provider);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetProviderBySlugAsync("test-slug");

        // Assert
        result.Should().NotBeNull();
        result!.Slug.Should().Be("test-slug");
    }

    [Fact]
    public async Task GetProviderBySlugAsync_WhenProviderDoesNotExist_ReturnsNull()
    {
        // Act
        var result = await _sut.GetProviderBySlugAsync("non-existent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SetProviderEnabledAsync_WhenProviderExists_UpdatesEnabledStatus()
    {
        // Arrange
        var provider = CreateTestProvider("test-provider", "Test Provider", enabled: false);
        _context.Providers.Add(provider);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.SetProviderEnabledAsync(provider.Id, enabled: true);

        // Assert
        result.Should().NotBeNull();
        result!.Enabled.Should().BeTrue();

        // Verify it's persisted
        var savedProvider = await _context.Providers.FindAsync(provider.Id);
        savedProvider!.Enabled.Should().BeTrue();
    }

    [Fact]
    public async Task SetProviderEnabledAsync_WhenProviderDoesNotExist_ReturnsNull()
    {
        // Act
        var result = await _sut.SetProviderEnabledAsync(Guid.NewGuid(), enabled: true);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UninstallProviderAsync_WhenProviderExists_RemovesProvider()
    {
        // Arrange
        var provider = CreateTestProvider("test-provider", "Test Provider");
        _context.Providers.Add(provider);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.UninstallProviderAsync(provider.Id);

        // Assert
        result.Should().BeTrue();

        // Verify it's deleted
        var deletedProvider = await _context.Providers.FindAsync(provider.Id);
        deletedProvider.Should().BeNull();
    }

    [Fact]
    public async Task UninstallProviderAsync_WhenProviderDoesNotExist_ReturnsFalse()
    {
        // Act
        var result = await _sut.UninstallProviderAsync(Guid.NewGuid());

        // Assert
        result.Should().BeFalse();
    }

    private static Provider CreateTestProvider(
        string slug,
        string name,
        bool enabled = true,
        string? repositoryUrl = null)
    {
        return new Provider(
            Id: Guid.NewGuid(),
            Slug: slug,
            Name: name,
            Version: "1.0.0",
            Enabled: enabled,
            RepositoryUrl: repositoryUrl ?? "https://github.com/test/provider",
            ImageUrl: null,
            GrpcEndpoint: $"timebase-{slug}:50051",
            Config: null,
            Capabilities: null,
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow
        );
    }
}
