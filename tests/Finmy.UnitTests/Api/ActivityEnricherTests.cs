using System.Diagnostics;

using Finmy.Api.Observability;

using Serilog;
using Serilog.Core;
using Serilog.Events;

using Shouldly;

namespace Finmy.UnitTests.Api;

public class ActivityEnricherTests
{
    private static readonly ActivitySource TestSource = new("Finmy.UnitTests.ActivityEnricher");

    [Fact]
    public void Enrich_WithActiveActivity_AddsTraceAndSpanIds()
    {
        using var listener = StartListening();
        using var activity = TestSource.StartActivity("test-activity");

        var logEvent = EmitLogEvent();

        logEvent.Properties["trace_id"].ToString().ShouldContain(activity!.TraceId.ToString());
        logEvent.Properties["span_id"].ToString().ShouldContain(activity.SpanId.ToString());
    }

    [Fact]
    public void Enrich_WithCorrelationTagSet_UsesCorrelationTagOverTraceId()
    {
        using var listener = StartListening();
        using var activity = TestSource.StartActivity("test-activity");
        activity!.SetTag(CorrelationIdMiddleware.TagName, "correlation-abc");

        var logEvent = EmitLogEvent();

        logEvent.Properties["correlation_id"].ToString().ShouldContain("correlation-abc");
    }

    [Fact]
    public void Enrich_WithoutCorrelationTag_FallsBackToTraceId()
    {
        using var listener = StartListening();
        using var activity = TestSource.StartActivity("test-activity");

        var logEvent = EmitLogEvent();

        logEvent.Properties["correlation_id"].ToString().ShouldContain(activity!.TraceId.ToString());
    }

    [Fact]
    public void Enrich_WithNoActiveActivity_AddsNoTraceProperties()
    {
        Activity.Current = null;

        var logEvent = EmitLogEvent();

        logEvent.Properties.ShouldNotContainKey("trace_id");
        logEvent.Properties.ShouldNotContainKey("span_id");
        logEvent.Properties.ShouldNotContainKey("correlation_id");
    }

    private static ActivityListener StartListening()
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        return listener;
    }

    private static LogEvent EmitLogEvent()
    {
        var sink = new CapturingSink();
        var logger = new LoggerConfiguration()
            .Enrich.With<ActivityEnricher>()
            .WriteTo.Sink(sink)
            .CreateLogger();

        logger.Information("probe");

        return sink.Captured ?? throw new InvalidOperationException("No log event was captured.");
    }

    private sealed class CapturingSink : ILogEventSink
    {
        public LogEvent? Captured { get; private set; }

        public void Emit(LogEvent logEvent) => Captured = logEvent;
    }
}
