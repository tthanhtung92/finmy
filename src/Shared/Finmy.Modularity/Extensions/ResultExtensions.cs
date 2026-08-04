using Finmy.SharedKernel.Results;

using Microsoft.AspNetCore.Http;

namespace Finmy.Modularity.Extensions;

public static class ResultExtensions
{
    /// <summary>
    /// Converts a generic Result into an HTTP IResult for ASP.NET Core Minimal APIs.
    /// </summary>
    /// <typeparam name="T">Type of the success value.</typeparam>
    /// <param name="result">The result to inspect.</param>
    /// <param name="onSuccess">Callback producing the HTTP response on success.</param>
    /// <returns>The result of onSuccess when successful, otherwise a ProblemDetails error.</returns>
    public static IResult Match<T>(this Result<T> result, Func<T, IResult> onSuccess)
    {
        // Branching, evaluated lazily:
        // - IsSuccess true: run the left branch only, passing the value to onSuccess (Results.Ok(user), say).
        // - IsSuccess false: skip onSuccess entirely and take the right branch to format the error.
        return result.IsSuccess ? onSuccess(result.Value) : result.ToProblemDetails();
    }

    /// <summary>
    /// Match overload for the non-generic Result
    /// </summary>
    /// <param name="result"></param>
    /// <param name="onSuccess"></param>
    /// <returns></returns>
    public static IResult Match(this Result result, Func<IResult> onSuccess)
    {
        return result.IsSuccess ? onSuccess() : result.ToProblemDetails();
    }

    public static IResult ToProblemDetails(this Result result)
    {
        if (result.IsSuccess)
            throw new InvalidOperationException("Succeeded Result can not create ProblemDetails.");

        var error = result.Error;
        var status = error.Type.ToStatusCode();
        var extensions = new Dictionary<string, object?>
        {
            ["errorCode"] = error.Code,
        };

        return Results.Problem(
            statusCode: status,
            detail: error.Description,
            extensions: extensions
        );
    }

    private static int ToStatusCode(this ErrorType type)
    {
        int statusCode = type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Failure => StatusCodes.Status500InternalServerError,
            ErrorType.Unprocessable => StatusCodes.Status422UnprocessableEntity,
            _ => StatusCodes.Status500InternalServerError
        };

        return statusCode;
    }
}
