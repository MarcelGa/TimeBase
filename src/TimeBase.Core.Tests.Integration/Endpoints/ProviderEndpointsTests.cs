using System.Net;
using System.Net.Http.Json;

using FluentAssertions;

using TimeBase.Core.Providers.Models;
using TimeBase.Core.Tests.Integration.Infrastructure;

namespace TimeBase.Core.Tests.Integration.Endpoints;

public class ProviderEndpointsTests : IClassFixture<TimeBaseWebApplicationFactory>, IAsyncLifetime
{
    private readonly TimeBaseWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ProviderEndpointsTests(TimeBaseWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        // Reset database after each test
        await _factory.ResetDatabaseAsync();
    }

    [Fact]
    public async Task GetAllProviders_ShouldReturnEmptyList_WhenNoProvidersExist()
    {
        // Act
        var response = await _client.GetAsync("/api/providers");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("providers");
    }

    [Fact]
    public async Task GetProviderById_ShouldReturnNotFound_WhenProviderDoesNotExist()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/providers/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task InstallProvider_ShouldReturnBadRequest_WhenUrlIsInvalid()
    {
        // Arrange
        var request = new InstallProviderRequest("not-a-url");

        // Act
        var response = await _client.PostAsJsonAsync("/api/providers", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task InstallProvider_ShouldReturnBadRequest_WhenUrlIsNotHttps()
    {
        // Arrange
        var request = new InstallProviderRequest("http://github.com/test/provider.git");

        // Act
        var response = await _client.PostAsJsonAsync("/api/providers", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task InstallProvider_ShouldReturnBadRequest_WhenUrlIsNotGitRepository()
    {
        // Arrange
        var request = new InstallProviderRequest("https://example.com/not-a-git-repo");

        // Act
        var response = await _client.PostAsJsonAsync("/api/providers", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Git");
    }

    [Fact]
    public async Task SetProviderEnabled_ShouldReturnNotFound_WhenProviderDoesNotExist()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var request = new SetProviderEnabledRequest(true);

        // Act
        var response = await _client.PatchAsJsonAsync($"/api/providers/{nonExistentId}/enabled", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UninstallProvider_ShouldReturnNotFound_WhenProviderDoesNotExist()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.DeleteAsync($"/api/providers/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}