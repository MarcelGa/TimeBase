using FluentValidation;

namespace TimeBase.Core.Infrastructure;

/// <summary>
/// Endpoint filter that automatically validates request objects using FluentValidation.
/// 
/// Usage: .AddEndpointFilter&lt;ValidationFilter&lt;TRequest&gt;&gt;()
/// 
/// This filter works best with POST/PUT/PATCH endpoints where the request object
/// is bound from the request body. For GET endpoints with query parameters that
/// are manually constructed in the handler, manual validation is still required.
/// 
/// Example:
/// <code>
/// // ✅ Works automatically (request from body)
/// app.MapPost("/api/providers", async (InstallProviderRequest request, ...) => { })
///    .AddEndpointFilter&lt;ValidationFilter&lt;InstallProviderRequest&gt;&gt;();
/// 
/// // ❌ Requires manual validation (request constructed in handler)
/// app.MapGet("/api/data/{symbol}", async (string symbol, string interval, ...) => {
///     var request = new GetDataRequest(symbol, interval); // Constructed manually
///     // Need to inject IValidator and validate manually here
/// })
/// </code>
/// </summary>
public class ValidationFilter<T> : IEndpointFilter where T : class
{
    private readonly IValidator<T>? _validator;

    public ValidationFilter(IServiceProvider serviceProvider)
    {
        // Try to resolve validator - it's optional
        _validator = serviceProvider.GetService<IValidator<T>>();
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        // If no validator is registered, just continue
        if (_validator == null)
        {
            return await next(context);
        }

        // Find the request object of type T in the arguments
        var requestObject = context.Arguments.OfType<T>().FirstOrDefault();
        if (requestObject == null)
        {
            return await next(context);
        }

        // Validate the request
        var validationResult = await _validator.ValidateAsync(requestObject);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray()
                );
            return Results.ValidationProblem(errors);
        }

        // Validation passed, continue to endpoint
        return await next(context);
    }
}
