using System.Reflection;

using Finmy.SharedKernel.Observability;

using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using StackExchange.Redis;

namespace Finmy.Api.Observability;

/// <summary>
/// Wires OpenTelemetry tracing and metrics for the whole host: ASP.NET Core, HttpClient, Npgsql,
/// Redis and Wolverine instrumentation, plus the anti-overspend <c>ActivitySource</c>/<c>Meter</c>
/// from <see cref="FinmyTelemetry"/>. The OTLP exporter is added
/// only when <c>OpenTelemetry:OtlpEndpoint</c> is configured, so CI and a bare `dotnet run` with
/// no collector stay quiet instead of failing to connect on every export interval.
/// </summary>
public static class ObservabilityExtensions
{
    private const string ServiceName = "finmy-api";

    public static IHostApplicationBuilder AddObservability(this IHostApplicationBuilder builder, IConnectionMultiplexer redisMultiplexer)
    {
        var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"];
        var serviceVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";

        var otel = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(ServiceName, serviceVersion: serviceVersion));

        otel.WithTracing(tracing =>
        {
            tracing
                .AddAspNetCoreInstrumentation(options =>
                    options.Filter = context => !context.Request.Path.StartsWithSegments("/health"))
                .AddHttpClientInstrumentation()
                .AddRedisInstrumentation(redisMultiplexer)
                .AddSource("Npgsql")
                .AddSource("Wolverine")
                .AddSource(FinmyTelemetry.AntiOverspendSourceName);

            if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            {
                tracing.AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otlpEndpoint));
            }
        });

        otel.WithMetrics(metrics =>
        {
            metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter("Wolverine")
                .AddMeter(FinmyTelemetry.MeterName);

            if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            {
                metrics.AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otlpEndpoint));
            }
        });

        return builder;
    }
}
