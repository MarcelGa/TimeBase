using FluentValidation;

using Microsoft.AspNetCore.Mvc;

namespace TimeBase.Core.Data.Models;

/// <summary>
/// Request parameters for getting historical data.
/// Uses [AsParameters] to bind from both route and query string.
/// </summary>
public record GetHistoricalDataRequest(
    [FromRoute] string Symbol,
    [FromQuery] string Provider,
    [FromQuery] string? Interval,
    [FromQuery] DateTime? Start,
    [FromQuery] DateTime? End
);

public class GetHistoricalDataRequestValidator : AbstractValidator<GetHistoricalDataRequest>
{
    private static readonly string[] ValidIntervals =
    {
        "1m", "5m", "15m", "30m",
        "1h", "4h",
        "1d",
        "1wk", "1mo"
    };

    public GetHistoricalDataRequestValidator()
    {
        RuleFor(x => x.Symbol)
            .NotEmpty().WithMessage("Symbol is required")
            .Matches(@"^[A-Z0-9\-\.:]+$").WithMessage("Symbol must contain only uppercase letters, numbers, hyphens, dots, and colons")
            .MaximumLength(50).WithMessage("Symbol must be 50 characters or less");

        RuleFor(x => x.Provider)
            .NotEmpty().WithMessage("Provider is required")
            .Matches(@"^[a-z0-9\-]+$").WithMessage("Provider must contain only lowercase letters, numbers, and hyphens")
            .MaximumLength(100).WithMessage("Provider must be 100 characters or less");

        RuleFor(x => x.Interval)
            .Must(BeValidInterval).WithMessage($"Interval must be one of: {string.Join(", ", ValidIntervals)}")
            .When(x => !string.IsNullOrEmpty(x.Interval));

        RuleFor(x => x.Start)
            .Must(date => !date.HasValue || date.Value <= DateTime.UtcNow.AddDays(1))
            .WithMessage("Start date cannot be in the future")
            .Must(BeReasonableStartDate).WithMessage("Start date cannot be more than 10 years in the past")
            .When(x => x.Start.HasValue);

        RuleFor(x => x.End)
            .Must(date => !date.HasValue || date.Value <= DateTime.UtcNow.AddDays(1))
            .WithMessage("End date cannot be in the future")
            .Must((request, end) => !request.Start.HasValue || !end.HasValue || request.Start.Value <= end.Value)
            .WithMessage("End date must be after or equal to start date")
            .Must((request, end) => !request.Start.HasValue || !end.HasValue || (end.Value - request.Start.Value).TotalDays <= 3650)
            .WithMessage("Date range cannot exceed 10 years")
            .When(x => x.End.HasValue);
    }

    private bool BeValidInterval(string? interval)
    {
        if (string.IsNullOrEmpty(interval)) return true;
        return ValidIntervals.Contains(interval, StringComparer.OrdinalIgnoreCase);
    }

    private bool BeReasonableStartDate(DateTime? date)
    {
        if (!date.HasValue) return true;
        return date.Value >= DateTime.UtcNow.AddYears(-10);
    }
}