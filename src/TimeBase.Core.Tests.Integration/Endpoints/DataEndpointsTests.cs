using System.Net;

using FluentAssertions;

using TimeBase.Core.Tests.Integration.Infrastructure;

namespace TimeBase.Core.Tests.Integration.Endpoints;

public class DataEndpointsTests : IClassFixture<TimeBaseWebApplicationFactory>, IAsyncLifetime
{
    private readonly TimeBaseWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public DataEndpointsTests(TimeBaseWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _factory.ResetDatabaseAsync();
    }

    [Fact]
    public async Task GetHistoricalData_ShouldReturnNotFound_WhenProviderSlugNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/providers/non-existent-slug/data/AAPL?interval=1d");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("non-existent-slug");
    }

    [Fact]
    public async Task GetHistoricalData_ShouldReturnNotFound_WhenSymbolIsMissing()
    {
        // Act - path should have provider slug and symbol, missing symbol means route doesn't match
        var response = await _client.GetAsync("/api/providers/test-provider/data/?interval=1d");

        // Assert - missing path segment means route not found
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetHistoricalData_ShouldReturnBadRequest_WhenIntervalIsInvalid()
    {
        // Act
        var response = await _client.GetAsync("/api/providers/test-provider/data/AAPL?interval=invalid");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetHistoricalData_ShouldReturnBadRequest_WhenStartDateIsInFuture()
    {
        // Arrange
        var futureDate = DateTime.UtcNow.AddDays(7).ToString("yyyy-MM-dd");

        // Act
        var response = await _client.GetAsync($"/api/providers/test-provider/data/AAPL?interval=1d&start={futureDate}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetHistoricalData_ShouldReturnBadRequest_WhenEndDateIsBeforeStartDate()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-10).ToString("yyyy-MM-dd");
        var endDate = DateTime.UtcNow.AddDays(-20).ToString("yyyy-MM-dd");

        // Act
        var response = await _client.GetAsync($"/api/providers/test-provider/data/AAPL?interval=1d&start={startDate}&end={endDate}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetHistoricalData_ShouldReturnNotFound_WhenProviderDoesNotExist()
    {
        // Act - use a provider slug that doesn't exist in the database
        var response = await _client.GetAsync("/api/providers/test-provider/data/AAPL?interval=1d");

        // Assert - provider doesn't exist, should return NotFound
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetDataSummary_ShouldReturnNotFound_WhenSymbolHasNoData()
    {
        // Act
        var response = await _client.GetAsync("/api/data/NONEXISTENT/summary");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetProvidersForSymbol_ShouldReturnOk_WhenValidSymbol()
    {
        // Act
        var response = await _client.GetAsync("/api/data/AAPL/providers");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("providers");
    }
}