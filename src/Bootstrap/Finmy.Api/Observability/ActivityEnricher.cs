using System.Diagnostics;

using Serilog.Core;
using Serilog.Events;

namespace Finmy.Api.Observability;

/// <summary>
/// Stamps every log event with the current trace, span and correlation ids so a log line can be
/// joined to a Tempo trace and to <see cref="CorrelationIdMiddleware"/>'s response header.
/// Handwritten rather than pulling in Serilog.Enrichers.Span: three fields off
/// <see cref="Activity.Current"/> does not earn a dependency.
/// </summary>
public sealed class ActivityEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var activity = Activity.Current;
        if (activity is null)
        {
            return;
        }

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("trace_id", activity.TraceId.ToString()));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("span_id", activity.SpanId.ToString()));

        var correlationId = activity.GetTagItem(CorrelationIdMiddleware.TagName) as string ?? activity.TraceId.ToString();
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("correlation_id", correlationId));
    }
}
