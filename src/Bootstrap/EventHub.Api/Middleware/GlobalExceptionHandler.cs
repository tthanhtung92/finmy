using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace EventHub.Api.Middleware;

internal sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled exception cho {Path}", httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        await problemDetailsService.TryWriteAsync(
            new ProblemDetailsContext 
            { 
                HttpContext = httpContext,
                ProblemDetails = new ProblemDetails 
                { 
                    Status = httpContext.Response.StatusCode,
                    Title = "An unexpected error occurred",
                    Detail = "The request could not be processed. Please try again later."
                }
            }
        );

        return true;
    }
}
