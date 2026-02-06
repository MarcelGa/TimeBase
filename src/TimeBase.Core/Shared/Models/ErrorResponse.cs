namespace TimeBase.Core.Shared.Models;

/// <summary>
/// Generic error response used across all endpoints.
/// </summary>
public record ErrorResponse(
    string Error
);