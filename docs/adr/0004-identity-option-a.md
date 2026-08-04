# ADR-0004: Identity module boundary via Option A (dependency inversion through IIdentityService)

## Status

Accepted, 2026-07-12

## Context

The Identity module uses ASP.NET Core Identity (`Microsoft.AspNetCore.Identity.EntityFrameworkCore`). That creates an architectural snag: login and registration handlers live in the Application layer and need `UserManager<ApplicationUser>`, but `UserManager` and `ApplicationUser` are types welded to the Identity framework and EF. Where they go decides whether two rules survive:

- **The DDD rule:** Domain should be plain POCOs with no framework attachment.
- **The layering rule:** Application must not depend on Infrastructure. Dependencies run Infrastructure to Application to Domain.

The familiar compromise is to push `ApplicationUser` down into Domain so Application can reach it. That sounds reasonable, but it patches layering by contaminating the Domain: it trades the DDD rule away to save the layering rule.

## Options considered

**The "entity in Domain" compromise.** Put `ApplicationUser` in Domain so Application can see it directly. Simple, but the Domain stops being plain POCOs, since it now pulls Identity and EF packages into the core, and the choice amounts to sacrificing one rule for the other.

**Option A, dependency inversion through `IIdentityService`.** Insert an abstraction: an `IIdentityService` interface declared in Application with a surface of primitives, and an implementation in Infrastructure that holds `UserManager`. This is what larger systems do (Jason Taylor's Clean Architecture template, eShopOnWeb). Neither rule is sacrificed; the cost is one adapter layer.

## Decision

Option A. Specifically:

- `ApplicationUser`, `ApplicationRole`, `IdentityDbContext` and the `IdentityService` implementation live in **`Finmy.Identity.Infrastructure`**, treating auth as infrastructure.
- `RefreshToken` is a plain POCO in **`Finmy.Identity.Domain`**, holding only a `Guid UserId` as an id reference, with no navigation back to `ApplicationUser`.
- The **`IIdentityService`** interface lives in **`Finmy.Identity.Application`**, so the abstraction points upward. Its surface is primitives (`string`, `bool`, `Result`, `userId`) and exposes no Identity types.
- Primary keys are `Guid`: `ApplicationUser : IdentityUser<Guid>`, `ApplicationRole : IdentityRole<Guid>`, context `IdentityDbContext<ApplicationUser, ApplicationRole, Guid>`.
- `AddIdentityCore` rather than `AddIdentity`, since auth goes through JWT and needs no cookies or UI.

The rule in short: abstractions point up into Application, implementations point down into Infrastructure, and DI in the Bootstrap host joins them at runtime.

## Consequences

The Domain stays strictly POCO with no Identity or EF packages, and Application never references Infrastructure. The reference direction is exactly Infrastructure to Application to Domain. Handlers inject `IIdentityService` and never learn that `UserManager` exists, which is something NetArchTest can verify.

The cost is one adapter layer (interface plus implementation) with a primitive surface, so every new Identity capability adds a method to `IIdentityService`. That cost is real but small, and it buys a core fully detached from the auth framework.

`Guid` keys mean the `InitialCreate` migration has to build uuid primary keys from the start. In exchange, sequential numbers are not exposed and the keys suit cross-module id references later.
