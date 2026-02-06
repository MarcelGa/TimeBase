using FluentValidation;

namespace TimeBase.Core.Providers.Models;

public record SetProviderEnabledRequest(bool Enabled);

public class SetProviderEnabledRequestValidator : AbstractValidator<SetProviderEnabledRequest>
{
    public SetProviderEnabledRequestValidator()
    {
        // Enabled is a boolean, so it's always valid
        // This validator exists for consistency and future extension
    }
}