using FluentValidation;

namespace TimeBase.Core.Shared.Filters;

/// <summary>
/// Global endpoint filter that automatically validates all request objects using FluentValidation.
/// 
/// This filter is registered globally and will automatically validate any endpoint argument
/// that has a corresponding IValidator&lt;T&gt; registered in the dependency injection container.
/// 
/// Setup:
/// <code>
/// // 1. Register validators automatically (in Program.cs)
/// builder.Services.AddValidatorsFromAssemblyContaining&lt;Program&gt;();
/// 
/// // 2. Register this global filter (in Program.cs)
/// builder.Services.AddSingleton&lt;GlobalValidationFilter&gt;();
/// 
/// // 3. Apply to all API endpoints (in Program.cs)
/// var api = app.MapGroup("/api").AddEndpointFilter&lt;GlobalValidationFilter&gt;();
/// </code>
/// 
/// How it works:
/// - For each endpoint invocation, inspects all arguments
/// - Checks if a validator is registered for each argument type
/// - If a validator exists, runs validation automatically
/// - Returns 400 Bad Request with validation errors if validation fails
/// - Otherwise continues to the endpoint handler
/// 
/// Example:
/// <code>
/// // ✅ This endpoint will be automatically validated if InstallProviderRequestValidator exists
/// app.MapPost("/api/providers", async (InstallProviderRequest request, ...) => { })
/// 
/// // ✅ No need to manually add .AddEndpointFilter&lt;ValidationFilter&lt;T&gt;&gt;()
/// // ✅ Just create a validator class and it will be picked up automatically
/// </code>
/// 
/// Benefits:
/// - No need to manually add validation filters to each endpoint
/// - Consistent validation behavior across all endpoints
/// - Follows FluentValidation recommended DI patterns
/// - Type-safe and maintainable
/// </summary>
public class GlobalValidationFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        // Iterate through all endpoint arguments and validate if validators exist
        foreach (var argument in context.Arguments)
        {
            if (argument == null)
            {
                continue;
            }

            var argumentType = argument.GetType();

            // Skip primitive types and common framework types that won't have validators
            if (argumentType.IsPrimitive ||
                argumentType == typeof(string) ||
                argumentType == typeof(Guid) ||
                argumentType == typeof(DateTime) ||
                argumentType == typeof(CancellationToken) ||
                argumentType.Namespace?.StartsWith("Microsoft.") == true ||
                argumentType.Namespace?.StartsWith("System.") == true)
            {
                continue;
            }

            // Try to get validator from DI container
            var validatorType = typeof(IValidator<>).MakeGenericType(argumentType);
            var validator = context.HttpContext.RequestServices.GetService(validatorType) as IValidator;

            if (validator == null)
            {
                continue; // No validator registered, skip validation
            }

            // Use IValidator.ValidateAsync directly (non-generic interface)
            var validationContext = new ValidationContext<object>(argument);
            var validationResult = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);

            if (validationResult != null && !validationResult.IsValid)
            {
                // Build error dictionary for validation problem response
                var errors = validationResult.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(e => e.ErrorMessage).ToArray()
                    );

                return Results.ValidationProblem(errors);
            }
        }

        // All validations passed, continue to endpoint handler
        return await next(context);
    }
}