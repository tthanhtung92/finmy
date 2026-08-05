using System.Diagnostics;

namespace Finmy.Api.Observability;

/// <summary>
/// Accepts an inbound <c>X-Correlation-ID</c>, generates one when absent, stamps it on the
/// current <see cref="Activity"/> so <see cref="ActivityEnricher"/> can put it on every log line
/// for the request, and echoes it back on the response so a caller can grep for their own request.
/// Registered first in the pipeline, ahead of response compression, so it wraps everything else.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";
    public const string TagName = "correlation.id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var header) && !string.IsNullOrWhiteSpace(header)
            ? header.ToString()
            : Guid.CreateVersion7().ToString();

        Activity.Current?.SetTag(TagName, correlationId);
        context.Response.Headers[HeaderName] = correlationId;

        await next(context);
    }
}
