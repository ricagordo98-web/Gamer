using FluentValidation;
using FluentValidation.Results;

namespace Gamer.Api.Infrastructure;

public class ValidationFilter<T>(IValidator<T> validator) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        T? argument = context.Arguments.OfType<T>().FirstOrDefault();
        if (argument is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { [typeof(T).Name] = ["Request body is required."] });
        }

        ValidationResult result = await validator.ValidateAsync(argument, context.HttpContext.RequestAborted);
        if (!result.IsValid)
        {
            return Results.ValidationProblem(result.ToDictionary());
        }

        return await next(context);
    }
}
