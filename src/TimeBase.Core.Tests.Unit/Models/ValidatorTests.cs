using FluentValidation.TestHelper;

using TimeBase.Core.Data.Models;
using TimeBase.Core.Providers.Models;

namespace TimeBase.Core.Tests.Unit.Models;

public class InstallProviderRequestValidatorTests
{
    private readonly InstallProviderRequestValidator _validator = new();

    [Theory]
    [InlineData("https://github.com/user/repo")]
    [InlineData("https://github.com/user/repo.git")]
    [InlineData("https://gitlab.com/user/repo")]
    [InlineData("https://bitbucket.org/user/repo")]
    [InlineData("https://github.com/org/project/subproject")]
    public async Task Validate_WithValidGitUrl_ShouldNotHaveErrors(string url)
    {
        // Arrange
        var request = new InstallProviderRequest(url);

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Validate_WithEmptyOrNullUrl_ShouldHaveError(string? url)
    {
        // Arrange
        var request = new InstallProviderRequest(url!);

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Repository);
    }

    [Theory]
    [InlineData("invalid-url")]
    [InlineData("ftp://github.com/user/repo")]
    [InlineData("http://github.com/user/repo")]
    [InlineData("not a url at all")]
    public async Task Validate_WithInvalidUrl_ShouldHaveError(string url)
    {
        // Arrange
        var request = new InstallProviderRequest(url);

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Repository);
    }

    [Theory]
    [InlineData("https://example.com/user/repo")]
    [InlineData("https://google.com")]
    public async Task Validate_WithNonGitHostUrl_ShouldHaveError(string url)
    {
        // Arrange
        var request = new InstallProviderRequest(url);

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Repository)
            .WithErrorMessage("Repository must be a Git repository URL (GitHub, GitLab, Bitbucket)");
    }
}

public class GetHistoricalDataRequestValidatorTests
{
    private readonly GetHistoricalDataRequestValidator _validator = new();

    [Fact]
    public async Task Validate_WithValidRequest_ShouldNotHaveErrors()
    {
        // Arrange
        var request = new GetHistoricalDataRequest(
            Symbol: "AAPL",
            Provider: "test-provider",
            Interval: "1d",
            Start: DateTime.UtcNow.AddDays(-30),
            End: DateTime.UtcNow
        );

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Validate_WithEmptyOrNullSymbol_ShouldHaveError(string? symbol)
    {
        // Arrange
        var request = new GetHistoricalDataRequest(
            Symbol: symbol!,
            Provider: "test-provider",
            Interval: "1d",
            Start: null,
            End: null
        );

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Symbol);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Validate_WithEmptyOrNullProvider_ShouldHaveError(string? provider)
    {
        // Arrange
        var request = new GetHistoricalDataRequest(
            Symbol: "AAPL",
            Provider: provider!,
            Interval: "1d",
            Start: null,
            End: null
        );

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Provider);
    }

    [Theory]
    [InlineData("Invalid Provider")]
    [InlineData("UPPERCASE")]
    [InlineData("has_underscore")]
    [InlineData("has.dot")]
    public async Task Validate_WithInvalidProviderFormat_ShouldHaveError(string provider)
    {
        // Arrange
        var request = new GetHistoricalDataRequest(
            Symbol: "AAPL",
            Provider: provider,
            Interval: "1d",
            Start: null,
            End: null
        );

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Provider);
    }

    [Theory]
    [InlineData("1m")]
    [InlineData("5m")]
    [InlineData("15m")]
    [InlineData("30m")]
    [InlineData("1h")]
    [InlineData("4h")]
    [InlineData("1d")]
    [InlineData("1wk")]
    [InlineData("1mo")]
    public async Task Validate_WithValidInterval_ShouldNotHaveError(string interval)
    {
        // Arrange
        var request = new GetHistoricalDataRequest(
            Symbol: "AAPL",
            Provider: "test-provider",
            Interval: interval,
            Start: null,
            End: null
        );

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Interval);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("2m")]
    [InlineData("10d")]
    [InlineData("1y")]
    public async Task Validate_WithInvalidInterval_ShouldHaveError(string interval)
    {
        // Arrange
        var request = new GetHistoricalDataRequest(
            Symbol: "AAPL",
            Provider: "test-provider",
            Interval: interval,
            Start: null,
            End: null
        );

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Interval);
    }

    [Fact]
    public async Task Validate_WithFutureStartDate_ShouldHaveError()
    {
        // Arrange
        var request = new GetHistoricalDataRequest(
            Symbol: "AAPL",
            Provider: "test-provider",
            Interval: "1d",
            Start: DateTime.UtcNow.AddDays(10),
            End: DateTime.UtcNow.AddDays(20)
        );

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Start);
    }

    [Fact]
    public async Task Validate_WithEndBeforeStart_ShouldHaveError()
    {
        // Arrange
        var request = new GetHistoricalDataRequest(
            Symbol: "AAPL",
            Provider: "test-provider",
            Interval: "1d",
            Start: DateTime.UtcNow.AddDays(-10),
            End: DateTime.UtcNow.AddDays(-20)
        );

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.End);
    }

    [Fact]
    public async Task Validate_WithDateRangeExceeding10Years_ShouldHaveError()
    {
        // Arrange
        var request = new GetHistoricalDataRequest(
            Symbol: "AAPL",
            Provider: "test-provider",
            Interval: "1d",
            Start: DateTime.UtcNow.AddYears(-11),
            End: DateTime.UtcNow
        );

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.End);
    }
}

public class SetProviderEnabledRequestValidatorTests
{
    private readonly SetProviderEnabledRequestValidator _validator = new();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Validate_WithValidRequest_ShouldNotHaveErrors(bool enabled)
    {
        // Arrange
        var request = new SetProviderEnabledRequest(enabled);

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}