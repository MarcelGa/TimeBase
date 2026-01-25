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
    public async Task GetHistoricalData_ShouldReturnBadRequest_WhenIntervalIsMissing()
    {
        // Act
        var response = await _client.GetAsync("/api/data/historical?symbol=AAPL");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetHistoricalData_ShouldReturnBadRequest_WhenIntervalIsInvalid()
    {
        // Act
        var response = await _client.GetAsync("/api/data/historical?symbol=AAPL&interval=invalid");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetHistoricalData_ShouldReturnBadRequest_WhenStartDateIsInFuture()
    {
        // Arrange
        var futureDate = DateTime.UtcNow.AddDays(7).ToString("yyyy-MM-dd");

        // Act
        var response = await _client.GetAsync($"/api/data/historical?symbol=AAPL&interval=1d&start={futureDate}");

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
        var response = await _client.GetAsync($"/api/data/historical?symbol=AAPL&interval=1d&start={startDate}&end={endDate}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetHistoricalData_ShouldReturnOk_WhenValidRequest()
    {
        // Act
        var response = await _client.GetAsync("/api/data/AAPL?interval=1d");

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
