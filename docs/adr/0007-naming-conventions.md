# ADR-0007: Settle naming conventions for folders, files and namespaces

## Status

Accepted, 2026-07-20

## Context

By the time the Budgeting module took shape, naming inside the projects had drifted enough to make finding files annoying. Interfaces sat in `Abstractions/` in two modules' Application layers but in `Interfaces/` in `Finmy.Modularity`. The DTO folder was singular, `Dto/`. Budgeting's service was plural, `EnvelopesService`, while Identity's was singular, `AuthService`. Identity's namespaces repeated the module name twice: `Finmy.Identity.Domain.Identity`, `Finmy.Identity.Infrastructure.Identity`. `ValidationFilter` sat loose at the root of `Finmy.Modularity`. The contexts carried long names, `IdentityModuleDbContext` and `BudgetingModuleDbContext`.

None of that is wrong to the compiler, but together it meant opening an unfamiliar project and hunting for the file you wanted.

## Options considered

**Leave it and accept the drift.** Cheapest now, but every later module inherits the inconsistency and the cost of fixing it grows.

**Minimal standardisation**, resolving only the obvious contradictions (the interface folder, the plural service) and leaving the contexts and the repeated namespaces alone. Lower risk, but it leaves the job half done.

**Full standardisation**, settling one convention table and applying it everywhere: interfaces to `Abstractions/`, DTOs to `Dtos/`, singular service names, `Module` dropped from context names, the repeated namespaces split into `RefreshTokens/` and `Users/`, and `ValidationFilter` moved into `Filters/`.

## Decision

Full standardisation. The complete table and the reasoning per entry live in [naming-conventions.md](../naming-conventions.md), which is the reference; this record captures that the decision was made and why.

Three points are worth recording separately because they carry trade-offs.

**Interfaces collect in `Abstractions/`**, matching Microsoft's own habit (`*.Abstractions` assemblies). `Contracts` was not chosen because `Finmy.Contracts` already holds cross-module integration events, and reusing the word would blur two very different concepts.

**Context names drop `Module`.** For Identity that means our `IdentityDbContext` inherits ASP.NET Core Identity's `IdentityDbContext<TUser, TRole, TKey>`. The two share a simple name but differ in generic arity, so C# tells them apart and compilation is clean, and the base list states the parameters explicitly so nothing is ambiguous. That name collision is accepted in exchange for consistency with `BudgetingDbContext`.

**Renaming a context class is safe for data.** `__EFMigrationsHistory` keys on MigrationId, not on the class name, so a rename needs no `database update`. It only needs the name updated consistently in the snapshot and Designer files so the next `migrations add` does not produce a phantom diff. Existing migration filenames and MigrationIds stay as they are.

## Consequences

Opening any project makes it possible to guess where a file lives, and later modules follow the same tree.

There is a document to point at in review: if a name does not match the table, fix the table first, with a reason, and then the name.

The refactor only changed names and locations, not behaviour. The build stayed clean and Identity's model snapshot produced no changes. It did surface one pre-existing gap: `Category.Name` declares `HasMaxLength(200)` in `OnModelCreating` without a migration to match. That is outside the scope of a rename and is handled separately.
