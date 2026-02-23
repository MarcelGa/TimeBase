using Microsoft.AspNetCore.Diagnostics;

namespace TimeBase.Core.Shared.ExceptionHandlers;

/// <summary>
/// Global exception handler that logs unhandled exceptions.
/// Returns false to delegate response writing to the built-in
/// UseExceptionHandler middleware, which produces RFC 7807 ProblemDetails.
/// </summary>
public class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    /// <summary>
    /// Logs the unhandled exception. Returns false so the built-in
    /// exception handler middleware writes a standard ProblemDetails response.
    /// </summary>
    public ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled exception occurred: {Message}", exception.Message);

        // Return false to let UseExceptionHandler write the default ProblemDetails 500 response
        return ValueTask.FromResult(false);
    }
}