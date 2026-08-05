using Microsoft.AspNetCore.OutputCaching;

namespace Finmy.Budgeting.Api.Caching;

/// <summary>
/// Output caching skips any request carrying an Authorization header by default -- the safety
/// net against serving one user's cached response to another (TECH-DEBT #12). Now that
/// /envelopes requires an authenticated user, that default would silently turn caching off.
/// This policy re-enables lookup and storage and varies the cache key by the caller's "sub"
/// claim, so each user gets their own cached slot instead of the request being served
/// uncached or, worse, from someone else's cache entry.
///
/// "sub" is used as a literal rather than IdentityClaimTypes.Sub: that constant lives in
/// Finmy.Identity.Infrastructure, and Budgeting referencing it would be exactly the
/// cross-module boundary violation ModuleBoundaryTests exists to catch. "sub" is a registered
/// JWT claim name fixed by RFC 7519, not by Identity's code -- see ADR-0005.
/// </summary>
public sealed class VaryByUserPolicy : IOutputCachePolicy
{
    public ValueTask CacheRequestAsync(OutputCacheContext context, CancellationToken cancellation)
    {
        context.AllowCacheLookup = true;
        context.AllowCacheStorage = true;
        context.CacheVaryByRules.VaryByValues["sub"] = context.HttpContext.User.FindFirst("sub")?.Value ?? string.Empty;

        return ValueTask.CompletedTask;
    }

    public ValueTask ServeFromCacheAsync(OutputCacheContext context, CancellationToken cancellation) =>
        ValueTask.CompletedTask;

    public ValueTask ServeResponseAsync(OutputCacheContext context, CancellationToken cancellation) =>
        ValueTask.CompletedTask;
}
