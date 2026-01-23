using FluentValidation;

namespace TimeBase.Core.Models;

public record InstallProviderRequest(string Repository);

public class InstallProviderRequestValidator : AbstractValidator<InstallProviderRequest>
{
    public InstallProviderRequestValidator()
    {
        RuleFor(x => x.Repository)
            .NotEmpty().WithMessage("Repository URL is required")
            .Must(BeValidUrl).WithMessage("Repository must be a valid URL")
            .Must(BeGitUrl).WithMessage("Repository must be a Git repository URL (GitHub, GitLab, Bitbucket)");
    }

    private bool BeValidUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uriResult)
            && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
    }

    private bool BeGitUrl(string url)
    {
        var lowerUrl = url.ToLowerInvariant();
        return lowerUrl.Contains("github.com") 
            || lowerUrl.Contains("gitlab.com") 
            || lowerUrl.Contains("bitbucket.org")
            || lowerUrl.EndsWith(".git");
    }
}
