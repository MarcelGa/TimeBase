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
    public async Task GetHistoricalData_ShouldReturnBadRequest_WhenSymbolIsMissing()
    {
        // Act
        var response = await _client.GetAsync("/api/data/historical?interval=1d");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetHistoricalData_ShouldReturnBadRequest_WhenProviderIdIsMissing()
    {
        // Act
        var response = await _client.GetAsync("/api/data/AAPL?interval=1d");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("ProviderId");
    }

    [Fact]
    public async Task GetHistoricalData_ShouldReturnBadRequest_WhenProviderIdIsInvalid()
    {
        // Act
        var response = await _client.GetAsync("/api/data/AAPL?interval=1d&providerId=invalid-guid");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetHistoricalData_ShouldReturnBadRequest_WhenIntervalIsInvalid()
    {
        // Arrange
        var providerId = Guid.NewGuid();
        
        // Act
        var response = await _client.GetAsync($"/api/data/AAPL?interval=invalid&providerId={providerId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetHistoricalData_ShouldReturnBadRequest_WhenStartDateIsInFuture()
    {
        // Arrange
        var futureDate = DateTime.UtcNow.AddDays(7).ToString("yyyy-MM-dd");
        var providerId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/data/AAPL?interval=1d&start={futureDate}&providerId={providerId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetHistoricalData_ShouldReturnBadRequest_WhenEndDateIsBeforeStartDate()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-10).ToString("yyyy-MM-dd");
        var endDate = DateTime.UtcNow.AddDays(-20).ToString("yyyy-MM-dd");
        var providerId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/data/AAPL?interval=1d&start={startDate}&end={endDate}&providerId={providerId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetHistoricalData_ShouldReturnOk_WhenValidRequest()
    {
        // Arrange - use a random GUID as provider (will return empty data but should be 200 OK)
        var providerId = Guid.NewGuid();
        
        // Act
        var response = await _client.GetAsync($"/api/data/AAPL?interval=1d&providerId={providerId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("data");
    }

    [Fact]
    public async Task GetDataSummary_ShouldReturnBadRequest_WhenSymbolIsMissing()
    {
        // Act
        var response = await _client.GetAsync("/api/data/summary");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetProvidersForSymbol_ShouldReturnBadRequest_WhenSymbolIsMissing()
    {
        // Act
        var response = await _client.GetAsync("/api/data/providers");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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
