using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Finmy.Api.Middleware;

internal sealed partial class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        LogUnhandledException(logger, exception, httpContext.Request.Path);

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

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception for {Path}")]
    private static partial void LogUnhandledException(ILogger logger, Exception exception, PathString path);
}
