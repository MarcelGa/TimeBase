using System.Net;
using FluentAssertions;
using TimeBase.Core.Tests.Integration.Infrastructure;

namespace TimeBase.Core.Tests.Integration.Endpoints;

public class HealthCheckEndpointsTests : IClassFixture<TimeBaseWebApplicationFactory>
{
    private readonly TimeBaseWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public HealthCheckEndpointsTests(TimeBaseWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetHealthLive_ShouldReturnHealthy()
    {
        // Act
        var response = await _client.GetAsync("/health/live");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Be("Healthy");
    }

    [Fact]
    public async Task GetHealthReady_ShouldReturnHealthy_WhenDatabaseIsAvailable()
    {
        // Act
        var response = await _client.GetAsync("/health/ready");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Healthy");
    }

    [Fact]
    public async Task GetHealth_ShouldReturnDetailedHealthStatus()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("status");
        content.Should().Contain("Healthy");
    }
}
