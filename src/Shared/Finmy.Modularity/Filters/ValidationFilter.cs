using FluentValidation;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Finmy.Modularity.Filters;

public sealed class ValidationFilter<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var validator = httpContext.RequestServices.GetService<IValidator<T>>();

        if (validator is null)
            return await next(context);

        T? model = null;
        var args = context.Arguments;

        for (var i = 0; i < args.Count; i++)
        {
            if (args[i] is T candidate)
            {
                model = candidate;
                break;
            }
        }

        if (model is null)
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: "A request body is required.");

        var result = await validator.ValidateAsync(model, httpContext.RequestAborted);

        return result.IsValid
            ? await next(context)
            : TypedResults.ValidationProblem(result.ToDictionary());
    }
}