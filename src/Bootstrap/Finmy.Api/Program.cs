using Finmy.Api.Extensions;
using Finmy.Api.HealthChecks;
using Finmy.Api.Middleware;
using Finmy.Budgeting.Infrastructure.Persistence;
using Finmy.Identity.Infrastructure.Persistence;
using Finmy.Ledger.Infrastructure.Persistence;

using JasperFx;
using JasperFx.CodeGeneration;
using JasperFx.Core;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

using Scalar.AspNetCore;

using Wolverine;
using Wolverine.ErrorHandling;
using Wolverine.Postgresql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddModules(builder.Configuration);

builder.Services.AddOpenApi();
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});
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
    var connectionString = builder.Configuration.GetConnectionString("LedgerDb");
    opts.PersistMessagesWithPostgresql(connectionString!, "wolverine");
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

app.UseResponseCompression();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
app.UseOutputCache();
app.UseStaticFiles();
app.UseModules();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// The process is up; no dependency probing. Kubernetes/Docker use this to decide whether to
// restart the container, so it must never fail because a downstream dependency is unhealthy.
// AllowAnonymous/DisableRateLimiting: no fallback auth policy or rate limiter exists yet, but
// both land later this phase and a health probe must never be gated by either.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })
    .AllowAnonymous()
    .DisableRateLimiting();

// Ready to take traffic: probes Postgres (all three schemas, plus pending-migration detection
// for TECH-DEBT #16), Redis and S3/MinIO.
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") })
    .AllowAnonymous()
    .DisableRateLimiting();

return await app.RunJasperFxCommands(args);

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
