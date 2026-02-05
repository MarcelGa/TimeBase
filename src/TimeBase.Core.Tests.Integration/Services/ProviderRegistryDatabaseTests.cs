using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using TimeBase.Core.Infrastructure.Data;
using TimeBase.Core.Infrastructure.Entities;
using TimeBase.Core.Tests.Integration.Infrastructure;

namespace TimeBase.Core.Tests.Integration.Services;

public class ProviderRegistryDatabaseTests : IClassFixture<TimeBaseWebApplicationFactory>, IAsyncLifetime
{
    private readonly TimeBaseWebApplicationFactory _factory;
    private readonly IServiceScope _scope;
    private readonly TimeBaseDbContext _dbContext;

    public ProviderRegistryDatabaseTests(TimeBaseWebApplicationFactory factory)
    {
        _factory = factory;
        _scope = factory.Services.CreateScope();
        _dbContext = _scope.ServiceProvider.GetRequiredService<TimeBaseDbContext>();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _factory.ResetDatabaseAsync();
        _scope.Dispose();
    }

    [Fact]
    public async Task Provider_ShouldBePersisted_WhenAdded()
    {
        // Arrange
        var provider = new Provider(
            Guid.NewGuid(),
            "test-provider",
            "Test Provider",
            "1.0.0",
            true,
            "https://github.com/test/provider.git",
            "https://example.com/logo.png",
            "timebase-test-provider:50051",
            "{}",
            "{}",
            DateTime.UtcNow,
            DateTime.UtcNow
        );

        // Act
        _dbContext.Providers.Add(provider);
        await _dbContext.SaveChangesAsync();

        // Assert
        var savedProvider = await _dbContext.Providers.FindAsync(provider.Id);
        savedProvider.Should().NotBeNull();
        savedProvider!.Slug.Should().Be("test-provider");
        savedProvider.Name.Should().Be("Test Provider");
        savedProvider.Version.Should().Be("1.0.0");
        savedProvider.Enabled.Should().BeTrue();
        savedProvider.RepositoryUrl.Should().Be("https://github.com/test/provider.git");
    }

    [Fact]
    public async Task Provider_SlugIndex_ShouldEnforceUniqueness()
    {
        // Arrange
        var provider1 = new Provider(
            Guid.NewGuid(),
            "test-provider",
            "Test Provider 1",
            "1.0.0",
            true,
            "https://github.com/test/provider1.git",
            null,
            "timebase-test-provider:50051",
            "{}",
            "{}",
            DateTime.UtcNow,
            DateTime.UtcNow
        );

        var provider2 = new Provider(
            Guid.NewGuid(),
            "test-provider", // Same slug
            "Test Provider 2",
            "2.0.0",
            true,
            "https://github.com/test/provider2.git",
            null,
            "timebase-test-provider:50051",
            "{}",
            "{}",
            DateTime.UtcNow,
            DateTime.UtcNow
        );

        // Act
        _dbContext.Providers.Add(provider1);
        await _dbContext.SaveChangesAsync();

        _dbContext.Providers.Add(provider2);
        var act = async () => await _dbContext.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<Exception>(); // PostgreSQL will throw duplicate key error
    }

    [Fact]
    public async Task Symbol_ShouldBePersisted_WhenAdded()
    {
        // Arrange
        var symbol = new Symbol(
            Guid.NewGuid(),
            "AAPL",
            "Apple Inc.",
            "stock",
            "NASDAQ",
            "USD",
            "{}",
            DateTime.UtcNow
        );

        // Act
        _dbContext.Symbols.Add(symbol);
        await _dbContext.SaveChangesAsync();

        // Assert
        var savedSymbol = await _dbContext.Symbols.FindAsync(symbol.Id);
        savedSymbol.Should().NotBeNull();
        savedSymbol!.SymbolValue.Should().Be("AAPL");
        savedSymbol.Name.Should().Be("Apple Inc.");
        savedSymbol.Type.Should().Be("stock");
        savedSymbol.Exchange.Should().Be("NASDAQ");
        savedSymbol.Currency.Should().Be("USD");
    }

    [Fact]
    public async Task TimeSeriesData_ShouldBePersisted_WhenAdded()
    {
        // Arrange - First create a provider
        var provider = new Provider(
            Guid.NewGuid(),
            "yahoo-finance",
            "Yahoo Finance",
            "1.0.0",
            true,
            "https://github.com/test/yahoo.git",
            null,
            "timebase-yahoo-finance:50051",
            "{}",
            "{}",
            DateTime.UtcNow,
            DateTime.UtcNow
        );
        _dbContext.Providers.Add(provider);
        await _dbContext.SaveChangesAsync();

        // Create time series data
        var timeSeriesData = new TimeSeriesData(
            Time: DateTime.UtcNow.AddDays(-1),
            Symbol: "AAPL",
            ProviderId: provider.Id,
            Interval: "1d",
            Open: 150.0,
            High: 155.0,
            Low: 149.0,
            Close: 154.0,
            Volume: 1000000.0,
            Metadata: "{}"
        );

        // Act
        _dbContext.TimeSeries.Add(timeSeriesData);
        await _dbContext.SaveChangesAsync();

        // Assert
        var savedData = _dbContext.TimeSeries
            .Where(ts => ts.Symbol == "AAPL" && ts.ProviderId == provider.Id)
            .ToList();

        savedData.Should().NotBeEmpty();
        savedData[0].Symbol.Should().Be("AAPL");
        savedData[0].Open.Should().Be(150.0);
        savedData[0].High.Should().Be(155.0);
        savedData[0].Low.Should().Be(149.0);
        savedData[0].Close.Should().Be(154.0);
        savedData[0].Volume.Should().Be(1000000.0);
    }
}