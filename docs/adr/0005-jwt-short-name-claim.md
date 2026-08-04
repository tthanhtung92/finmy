# ADR-0005: Issue JWTs with short-name claims, with IdentityClaimTypes as the source of truth

## Status

Accepted, 2026-07-12

## Context

The Identity module issues JWT access tokens carrying identity (`sub`, `email`) and roles. That means choosing claim names and the handler pair that writes and reads tokens. .NET offers two naming schemes: the `ClaimTypes.*` set inherited from WIF, where every name is a long WS or SAML URI such as `http://schemas.xmlsoap.org/...`, and the short RFC 7519 JWT names (`sub`, `email`, `jti`, `exp`). Choosing wrong, or declaring names inconsistently between the issuing and reading sides, produces silent failures: nothing fails to compile, nothing throws, the system simply behaves incorrectly.

Two further decisions surfaced while building the generator and configuring `AddJwtBearer`: token expiry has to be testable, and a missing signing key has to stop the process early.

## Options considered

**`ClaimTypes.*`**, the reflexive choice, except that `ClaimTypes` has no `sub` member at all, and `ClaimTypes.Email` and `ClaimTypes.Role` are long WIF-era URIs. Tokens grow, reading them on jwt.io is painful, and the result diverges from the JWT standard.

**`System.IdentityModel.Tokens.Jwt`**, the legacy handler. Still usable, but a newer handler has replaced it.

**RFC 7519 short names through `JwtRegisteredClaimNames` plus `JsonWebTokenHandler`**, the modern handler from `Microsoft.IdentityModel.JsonWebTokens`. Compact, standard-conforming tokens. The catch is that `Role` is not part of the RFC, so `JwtRegisteredClaimNames` has no member for it and the name has to be chosen locally.

## Decision

Claims are issued with **short JWT names**: `sub` and `email` through `JwtRegisteredClaimNames`, and `role` as a locally chosen short name, since RFC 7519 does not standardise roles. The modern `JsonWebTokenHandler` is used, not the legacy handler.

Claim names are collected in a static `IdentityClaimTypes` class that acts as the source of truth, placed under `Authentication/` in Identity.Infrastructure and used on both the issuing and reading sides:

- `Sub` and `Email` are **aliases referencing** `JwtRegisteredClaimNames.*` rather than retyped literals, so no second magic string appears.
- `Role` is the literal `"role"`. This is the only place a literal is allowed, because the library provides no constant for it.

Having left the ASP.NET Core default (short names instead of long URLs), the reading side has to be told to match: set `TokenValidationParameters.RoleClaimType = IdentityClaimTypes.Role`, and `options.MapInboundClaims = false` on the options object rather than inside `TokenValidationParameters`.

Two decisions come along with it. Inject `TimeProvider` instead of using `DateTime.UtcNow` to compute `Expires`. Fail fast on the signing key symmetrically, on both the issuing and reading sides: a missing key throws at startup and never falls back to an empty string.

`IdentityClaimTypes` is deliberately not promoted to `SharedKernel` or `Contracts`. Claim names are a wire format internal to the Identity module, and exposing them would leak across a module boundary.

## Consequences

Tokens are compact, standard-conforming, and readable on jwt.io. A user with several roles produces several claims of type `"role"`, which the library serialises into a JSON array.

There is one source of truth for claim names, used at both issue and validation time, so a change happens in one place and the two sides cannot drift apart.

Leaving the defaults has a price in traps. Forgetting `RoleClaimType` makes `[Authorize(Roles=...)]` return 403 for users who do hold the role. Forgetting `MapInboundClaims = false` lets the handler remap `sub` to `nameidentifier`, so `User.FindFirst("sub")` returns null. Both fail silently and are hard to catch by hand, so they need end-to-end verification.

`TimeProvider` allows injecting a `FakeTimeProvider` to assert `exp` deterministically, independent of the test machine's clock.

Failing fast on the signing key means the app refuses to start without configuration, which beats running with an empty signature: validating against an empty key is close to no validation at all, and forged tokens pass easily.
