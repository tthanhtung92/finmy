using Finmy.Api.Extensions;
using Finmy.Api.HealthChecks;
using Finmy.Api.Middleware;
using Finmy.Api.Observability;
using Finmy.Budgeting.Infrastructure.Persistence;
using Finmy.Identity.Infrastructure.Persistence;
using Finmy.Ledger.Infrastructure.Persistence;
using Finmy.Modularity.Extensions;

using JasperFx;
using JasperFx.CodeGeneration;
using JasperFx.Core;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

using Scalar.AspNetCore;

using Serilog;
using Serilog.Formatting.Compact;

using StackExchange.Redis;

using System.Threading.RateLimiting;

using Wolverine;
using Wolverine.ErrorHandling;
using Wolverine.Postgresql;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, _, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.With<ActivityEnricher>()
        .WriteTo.Console(new CompactJsonFormatter());

    // Console always gets JSON; the OTLP sink (Loki, by way of the collector) is opt-in so a
    // bare `dotnet run` with no collector up does not spend every flush interval failing to
    // connect. Same gate as the trace/metric exporters below.
    var otlpEndpoint = context.Configuration["OpenTelemetry:OtlpEndpoint"];
    if (!string.IsNullOrWhiteSpace(otlpEndpoint))
    {
        configuration.WriteTo.OpenTelemetry(options =>
        {
            options.Endpoint = otlpEndpoint;
            // Without this, Loki's OTLP ingest labels every log line service_name
            // "unknown_service:dotnet" instead of matching the traces/metrics resource name,
            // which breaks the "one service" story in Grafana Explore.
            options.ResourceAttributes.Add("service.name", "finmy-api");
        });
    }
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddModules(builder.Configuration);

builder.Services.AddOpenApi();

// One multiplexer, shared by HybridCache's L2 and by the Redis trace instrumentation below,
// rather than the two connecting independently. AbortOnConnectFail = false matches today's
// tolerance of a Redis outage at startup: RedisHealthCheck is what actually reports it down.
var redisOptions = ConfigurationOptions.Parse(builder.Configuration.GetRequiredConnectionString("Redis"));
redisOptions.AbortOnConnectFail = false;
var redisMultiplexer = await ConnectionMultiplexer.ConnectAsync(redisOptions);
builder.Services.AddSingleton<IConnectionMultiplexer>(redisMultiplexer);

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.ConnectionMultiplexerFactory = () => Task.FromResult<IConnectionMultiplexer>(redisMultiplexer);
});
builder.AddObservability(redisMultiplexer);
builder.Services.AddHostedService<OutboxBacklogMetrics>();
builder.Services.AddHybridCache(options =>
{
    options.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(5),
        LocalCacheExpiration = TimeSpan.FromMinutes(1)
    };
});
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Partition by the authenticated user where there is one, falling back to remote IP for
    // anonymous requests (register/login/refresh) so one caller cannot starve another.
    // Read once at startup rather than through AddOptions/ValidateOnStart: these are operating
    // knobs, not fail-fast invariants, so a sensible default if unset is the right behaviour.
    var permitLimit = builder.Configuration.GetValue("RateLimiting:PermitLimit", 100);
    var windowSeconds = builder.Configuration.GetValue("RateLimiting:WindowSeconds", 60);
    var authPermitLimit = builder.Configuration.GetValue("RateLimiting:AuthPermitLimit", 10);
    var authWindowSeconds = builder.Configuration.GetValue("RateLimiting:AuthWindowSeconds", 60);

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirst("sub")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromSeconds(windowSeconds)
            }));

    // Login/register/refresh are the endpoints worth brute-forcing, so they get a tighter
    // named policy on top of the global limit rather than relying on the global limit alone.
    options.AddFixedWindowLimiter("auth", o =>
    {
        o.PermitLimit = authPermitLimit;
        o.Window = TimeSpan.FromSeconds(authWindowSeconds);
    });

    options.OnRejected = (context, cancellationToken) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        return ValueTask.CompletedTask;
    };
});
builder.Services.AddHealthChecks()
    .AddCheck<DbContextHealthCheck<IdentityDbContext>>("identity-db", tags: ["ready"])
    .AddCheck<DbContextHealthCheck<BudgetingDbContext>>("budgeting-db", tags: ["ready"])
    .AddCheck<DbContextHealthCheck<LedgerDbContext>>("ledger-db", tags: ["ready"])
    .AddCheck<RedisHealthCheck>("redis", tags: ["ready"])
    .AddCheck<S3HealthCheck>("s3", tags: ["ready"]);
builder.Services.CritterStackDefaults(x =>
{
    // Static needs a `codegen write` pass baked into the image or the host throws
    // ExpectedTypeMissingException on the first handler invocation; see ADR-0013.
    // Auto loads pre-built types when present and falls back to generating them.
    x.Production.GeneratedCodeMode = TypeLoadMode.Auto;
    x.Development.GeneratedCodeMode = TypeLoadMode.Dynamic;
});

builder.Host.UseWolverine(opts =>
{
    var connectionString = builder.Configuration.GetRequiredConnectionString("LedgerDb");
    opts.PersistMessagesWithPostgresql(connectionString, "wolverine");
    opts.Policies.AutoApplyTransactions();
    opts.Policies.UseDurableLocalQueues();

    opts.Policies.OnException<DbUpdateConcurrencyException>()
        .RetryWithCooldown(100.Milliseconds(), 250.Milliseconds(), 500.Milliseconds())
        .Then.MoveToErrorQueue();
    opts.Policies.OnException<DbUpdateException>()
        .Discard();

    opts.MultipleHandlerBehavior = MultipleHandlerBehavior.Separated;
    opts.Durability.MessageIdentity = MessageIdentity.IdAndDestination;
});

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();
app.UseResponseCompression();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.UseOutputCache();
app.UseStaticFiles();
app.UseModules();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}

// The process is up; no dependency probing. Kubernetes/Docker use this to decide whether to
// restart the container, so it must never fail because a downstream dependency is unhealthy.
// AllowAnonymous/DisableRateLimiting: rate limiting lands later this phase and a health probe
// must never be gated by it.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })
    .AllowAnonymous()
    .DisableRateLimiting();

// Ready to take traffic: probes Postgres (all three schemas, plus pending-migration detection
// for TECH-DEBT #16), Redis and S3/MinIO.
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") })
    .AllowAnonymous()
    .DisableRateLimiting();

try
{
    return await app.RunJasperFxCommands(args);
}
finally
{
    // The host does not end in app.Run(), so nothing else flushes the console/OTLP sinks
    // before the process exits.
    await Log.CloseAndFlushAsync();
}

/// <summary>
/// Top-level statements generate an internal Program, which WebApplicationFactory&lt;T&gt; cannot
/// see from another assembly. This declaration is what lets Finmy.IntegrationTests drive the
/// real host over HTTP instead of newing up a DbContext directly.
/// </summary>
public partial class Program
{
    // Never constructed. It exists only so the class is not a bag of static members with a
    // public constructor, and WebApplicationFactory<T> rules out making the class static.
    protected Program()
    {
    }
}
