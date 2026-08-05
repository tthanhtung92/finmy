# ADR-0016: Budgeting and Ledger require an authenticated user by default; the output cache varies by identity instead of being dropped

## Status

Accepted, 2026-08-05.

## Context

`docs/TECH-DEBT.md` item #1, marked as blocking production: Budgeting and Ledger endpoints had no `RequireAuthorization()` at all. Only two demo endpoints in `IdentityDemoEndpoints.cs` (`/identity/me`, `/identity/admin-only`) required auth; everything else, `POST /transactions`, all of `/envelopes`, all of `/receipts`, was anonymous. `AddAuthorization()` was called with no policy configured. Anyone could `POST /transactions` or read another user's envelope data.

Closing that gap had a direct, already-tracked consequence: TECH-DEBT #12. ASP.NET Core's output caching skips any request carrying an `Authorization` header by default, the framework's own safety net against serving one user's cached response to another. Once `GET /envelopes` and `GET /envelopes/summary` require auth, every request carries a bearer token, so that default would have silently turned caching off for both endpoints without anyone deciding to. The Phase 3 plan named this explicitly: decide drop-it versus vary-by-identity as part of the same change, not something to leave dangling.

## Options considered

**Add `.RequireAuthorization()` to each Budgeting and Ledger endpoint individually, leaving the default anonymous.** Rejected: opt-in means a new endpoint added later is anonymous by default unless someone remembers to add the call, which is the exact mistake that created TECH-DEBT #1. A repo-wide default that fails closed does not depend on anyone remembering.

**Drop output caching entirely on the two Budgeting endpoints instead of varying by identity.** Rejected: it silently removes a working, tested concept from the system (`docs/ROADMAP.md` section 4 lists caching as one of the backend concerns this project demonstrates) and changes what `bench/list-bench.js` measures without a deliberate decision behind it. Varying by identity keeps the concept intact at the cost of one extra claim lookup per cacheable request.

**Pass the JWT as an `Authorization` header on the SignalR connection, or drop SignalR auth entirely.** Rejected: a browser cannot set arbitrary headers on a WebSocket handshake, so an `Authorization` header is not available on this transport. SignalR's own documented pattern for this exact problem is an `access_token` query string parameter.

## Decision

`Identity.Infrastructure/DependencyInjection.cs` now sets `AddAuthorization`'s `FallbackPolicy` to `new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()`. Every endpoint requires an authenticated user unless it opts out with `.AllowAnonymous()`.

The anonymous allowlist: `POST /identity/register`, `/login`, `/refresh`, `/logout`; `GET /identity/ping`; `GET /health/live` and `/health/ready`; and, in the Development environment only, `MapOpenApi()`/`MapScalarApiReference()`. Logout stays anonymous on purpose. It revokes a refresh token the caller already holds in the request body, and requiring a live access token would make logout impossible once that token has already expired, exactly the situation where a user most needs to log out.

SignalR needed its own fix or the hub at `/api/v1/hubs/envelopes` would have become unreachable. `JwtBearerOptions.Events.OnMessageReceived` now reads an `access_token` query string parameter and sets `context.Token` from it, scoped to requests whose path starts with `/api/v1/hubs`, so a token in a URL, which ends up in server logs and browser history, stays limited to the one transport that has no other way to carry it.

For TECH-DEBT #12, `VaryByUserPolicy` (`Finmy.Budgeting.Api.Caching`) is a custom `IOutputCachePolicy` applied to both existing output-cache policies via `AddPolicy<VaryByUserPolicy>()`, alongside their existing `Expire`/`SetVaryByQuery`/`Tag` configuration in `BudgetingModule.cs`. It sets `AllowCacheLookup` and `AllowCacheStorage` back to `true`, then adds the caller's `sub` claim to `CacheVaryByRules.VaryByValues`, so each authenticated user gets an independent cache slot instead of the endpoint going uncached, or worse, one user's request being served from another user's cached entry. Tag-based invalidation is untouched: it still clears every vary-by-value slot for a tag when the underlying data changes, since it invalidates by tag, not by cache key.

`VaryByUserPolicy` uses the literal string `"sub"` rather than the `IdentityClaimTypes` constant that exists in `Finmy.Identity.Infrastructure.Authentication`, because Budgeting referencing anything in that namespace would be the cross-module boundary violation `ModuleBoundaryTests` exists to catch. `sub` is a registered JWT claim name fixed by RFC 7519, the Subject claim, so depending on the literal string means depending on the wire format, not on Identity's implementation.

Every HTTP client of Budgeting and Ledger now needs a valid bearer token, so `FinmyApiFactory` gained `CreateAuthenticatedClientAsync()`. It registers a fixed test user once per fixture, guarded by a `SemaphoreSlim` with the token cached after the first call, and logs in through the real `POST /api/v1/identity/login` endpoint rather than minting a JWT by hand from the signing key already set in `ConfigureWebHost`. Going through the real login path is worth more than a shortcut, since a bug there would otherwise go untested by every other HTTP test that depends on it. Every HTTP integration test that hits Budgeting or Ledger switched to it. `bench/list-bench.js` now logs in during `setup()` and sends the bearer token with every request it benchmarks.

## Consequences

TECH-DEBT #1 is closed. TECH-DEBT #12 is closed by choosing vary-by-identity over dropping the feature. Every existing caller of Budgeting or Ledger endpoints now needs a bearer token, a breaking change, and it forced the test-fixture plumbing described above, exactly what the Phase 3 plan anticipated.

Verified against real containers: unauthenticated `POST /transactions` and `GET /envelopes` both return `401`; register, then log in, then the same `GET` with a bearer token returns `200`; two different authenticated users each get independently cached `GET /envelopes` responses, and creating an envelope invalidates the tag globally, so both users' next request reflects the change rather than one being stuck on stale data.
