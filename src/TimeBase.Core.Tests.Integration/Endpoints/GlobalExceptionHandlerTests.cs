using System.Net;
using System.Text.Json;

using FluentAssertions;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using TimeBase.Core.Data.Models;
using TimeBase.Core.Data.Services;
using TimeBase.Core.Infrastructure.Entities;
using TimeBase.Core.Providers.Services;
using TimeBase.Core.Tests.Integration.Infrastructure;

namespace TimeBase.Core.Tests.Integration.Endpoints;

public class GlobalExceptionHandlerTests : IClassFixture<TimeBaseWebApplicationFactory>
{
    private readonly TimeBaseWebApplicationFactory _factory;

    public GlobalExceptionHandlerTests(TimeBaseWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClientWithThrowingProviderRegistry()
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IProviderRegistry>();
                services.AddScoped<IProviderRegistry, ThrowingProviderRegistry>();
            });
        }).CreateClient();
    }

    private HttpClient CreateClientWithThrowingDataCoordinator()
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDataCoordinator>();
                services.AddScoped<IDataCoordinator, ThrowingDataCoordinator>();
            });
        }).CreateClient();
    }

    [Fact]
    public async Task GetProviders_ShouldReturnProblemDetails500_WhenUnhandledExceptionOccurs()
    {
        // Arrange
        var client = CreateClientWithThrowingProviderRegistry();

        // Act
        var response = await client.GetAsync("/api/providers");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var content = await response.Content.ReadAsStringAsync();
        var problemDetails = JsonDocument.Parse(content).RootElement;

        problemDetails.GetProperty("status").GetInt32().Should().Be(500);
        problemDetails.GetProperty("title").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetProviders_ShouldNotLeakExceptionDetails_WhenUnhandledExceptionOccurs()
    {
        // Arrange
        var client = CreateClientWithThrowingProviderRegistry();

        // Act
        var response = await client.GetAsync("/api/providers");

        // Assert
        var content = await response.Content.ReadAsStringAsync();

        content.Should().NotContain("ThrowingProviderRegistry");
        content.Should().NotContain(ThrowingProviderRegistry.ExceptionMessage);
        content.Should().NotContain("stack trace", because: "stack traces should never be exposed to clients");
    }

    [Fact]
    public async Task UninstallProvider_ShouldReturnProblemDetails500_WhenUnhandledExceptionOccurs()
    {
        // Arrange
        var client = CreateClientWithThrowingProviderRegistry();

        // Act
        var response = await client.DeleteAsync("/api/providers/any-slug");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var content = await response.Content.ReadAsStringAsync();
        var problemDetails = JsonDocument.Parse(content).RootElement;

        problemDetails.GetProperty("status").GetInt32().Should().Be(500);
    }

    [Fact]
    public async Task GetDataSummary_ShouldReturnProblemDetails500_WhenUnhandledExceptionOccurs()
    {
        // Arrange
        var client = CreateClientWithThrowingDataCoordinator();

        // Act
        var response = await client.GetAsync("/api/data/AAPL/summary");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var content = await response.Content.ReadAsStringAsync();
        var problemDetails = JsonDocument.Parse(content).RootElement;

        problemDetails.GetProperty("status").GetInt32().Should().Be(500);
        problemDetails.GetProperty("title").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetHistoricalData_ShouldReturnProblemDetails500_WhenUnhandledExceptionOccurs()
    {
        // Arrange
        var client = CreateClientWithThrowingDataCoordinator();

        // Act
        var response = await client.GetAsync("/api/data/AAPL?provider=test&interval=1d&start=2024-01-01&end=2024-01-31");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var content = await response.Content.ReadAsStringAsync();
        var problemDetails = JsonDocument.Parse(content).RootElement;

        problemDetails.GetProperty("status").GetInt32().Should().Be(500);
    }

    /// <summary>
    /// Stub implementation of IProviderRegistry that throws on every method.
    /// Used to simulate unhandled exceptions in provider endpoints.
    /// </summary>
    private class ThrowingProviderRegistry : IProviderRegistry
    {
        public const string ExceptionMessage = "Simulated unhandled exception in provider registry";

        public Task<Provider> InstallProviderAsync(string repositoryUrl, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException(ExceptionMessage);

        public Task<List<Provider>> GetAllProvidersAsync(bool? enabled = null, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException(ExceptionMessage);

        public Task<Provider?> GetProviderBySlugAsync(string slug, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException(ExceptionMessage);

        public Task<bool> UninstallProviderAsync(string slug, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException(ExceptionMessage);

        public Task<Provider?> SetProviderEnabledAsync(string slug, bool enabled, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException(ExceptionMessage);

        public Task<Provider?> UpdateCapabilitiesAsync(string slug, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException(ExceptionMessage);

        public ProviderCapabilities? GetCachedCapabilities(Provider provider)
            => throw new InvalidOperationException(ExceptionMessage);

        public Task UpdateAllCapabilitiesAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException(ExceptionMessage);

        public Task<Dictionary<string, List<ProviderSymbol>>> GetAllSymbolsAsync(string? providerSlug = null, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException(ExceptionMessage);
    }

    /// <summary>
    /// Stub implementation of IDataCoordinator that throws on every method.
    /// Used to simulate unhandled exceptions in data endpoints.
    /// </summary>
    private class ThrowingDataCoordinator : IDataCoordinator
    {
        public const string ExceptionMessage = "Simulated unhandled exception in data coordinator";

        public Task<List<TimeSeriesData>> GetHistoricalAsync(
            string symbol, string interval, DateTime start, DateTime end, string providerSlug, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException(ExceptionMessage);

        public Task<List<Provider>> GetProvidersForSymbolAsync(string symbol, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException(ExceptionMessage);

        public Task<int> StoreTimeSeriesDataAsync(IEnumerable<TimeSeriesData> dataPoints, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException(ExceptionMessage);

        public Task<DataSummary?> GetDataSummaryAsync(string symbol, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException(ExceptionMessage);
    }
}