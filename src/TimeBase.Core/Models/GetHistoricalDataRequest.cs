using FluentValidation;

namespace TimeBase.Core.Models;

public record GetHistoricalDataRequest(
    string Symbol,
    string Interval,
    DateTime? Start,
    DateTime? End,
    Guid? ProviderId
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
            .Matches(@"^[A-Z0-9\-\.]+$").WithMessage("Symbol must contain only uppercase letters, numbers, hyphens, and dots")
            .MaximumLength(20).WithMessage("Symbol must be 20 characters or less");

        RuleFor(x => x.Interval)
            .NotEmpty().WithMessage("Interval is required")
            .Must(BeValidInterval).WithMessage($"Interval must be one of: {string.Join(", ", ValidIntervals)}");

        RuleFor(x => x)
            .Must(x => !x.Start.HasValue || !x.End.HasValue || x.Start.Value <= x.End.Value)
            .WithMessage("Start date must be before or equal to end date");

        RuleFor(x => x.Start)
            .Must(BeReasonableStartDate).WithMessage("Start date cannot be more than 10 years in the past")
            .When(x => x.Start.HasValue);

        RuleFor(x => x.End)
            .Must(date => date!.Value <= DateTime.UtcNow.AddDays(1))
            .WithMessage("End date cannot be in the future")
            .When(x => x.End.HasValue);
    }

    private bool BeValidInterval(string interval)
    {
        return ValidIntervals.Contains(interval, StringComparer.OrdinalIgnoreCase);
    }

    private bool BeReasonableStartDate(DateTime? date)
    {
        if (!date.HasValue) return true;
        return date.Value >= DateTime.UtcNow.AddYears(-10);
    }
}
