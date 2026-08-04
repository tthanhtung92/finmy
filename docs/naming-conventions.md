# Naming conventions for folders and files

This document settles how folders, files, namespaces and classes are named in Finmy. The goal is that opening any project makes it possible to guess where a file lives instead of hunting for it. It applies to every existing module and every module written later.

Reference points: Microsoft's [Framework Design Guidelines](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/), and the feature-folder approach described by [Milan Jovanović](https://milanjovanovic.tech/blog/clean-architecture-folder-structure) and [Anton Martyniuk](https://antondevtips.com/blog/how-to-structure-production-apps-with-vertical-slice-architecture-in-dotnet-in-2026).

## The table

| Element | Rule | Example |
| --- | --- | --- |
| Project | `Finmy.{Module}.{Layer}` | `Finmy.Budgeting.Application` |
| Feature folder | plural after the entity, or a noun for a capability | `Envelopes/`, `Categories/`, `RefreshTokens/`, `Users/`, `Authentication/` |
| Port or interface | collected in `Abstractions/` at the project root | `Application/Abstractions/IEnvelopeRepository.cs` |
| DTO (request, response, validator) | `{feature}/Dtos/` | `Envelopes/Dtos/CreateEnvelopeRequest.cs` |
| Service | singular, `Service` suffix | `EnvelopeService`, `AuthService` |
| Endpoints | `{Entity}Endpoints` | `EnvelopeEndpoints` |
| DbContext | `{Module}DbContext` | `BudgetingDbContext`, `IdentityDbContext` |
| Persistence | `Persistence/` holds the context, factories and repositories | `Infrastructure/Persistence/` |
| Filters and behaviours | grouped by role, never loose at the project root | `Filters/ValidationFilter.cs` |

## Why each choice

**`Abstractions/` for interfaces.** Microsoft packages interfaces into `*.Abstractions` assemblies (`Microsoft.Extensions.Logging.Abstractions`, for instance), so the name reads naturally to a .NET developer. The repo previously mixed `Abstractions/` in Application with `Interfaces/` in Modularity; both are now one name. `Contracts` was avoided because `Finmy.Contracts` already holds cross-module integration events, and reusing the word would conflate two very different concepts.

**Plural feature folders.** An `Envelopes/` folder gathers everything about envelopes: entity, errors, service, DTOs. If envelopes ever need extracting into a separate service, the folder moves as a unit. Plural because it holds a group of related things rather than a single class. `Authentication/` is a reasonable exception: it names a capability, not an entity.

**Singular service class names.** `EnvelopeService`, not `EnvelopesService`. A class is one thing, whereas a folder is a group. This also matches how .NET names services and handlers generally.

**Context names drop `Module`.** `BudgetingDbContext` is shorter than `BudgetingModuleDbContext` and still says which module it belongs to. For Identity our `IdentityDbContext` inherits ASP.NET Core Identity's `IdentityDbContext<TUser, TRole, TKey>`; the two share a simple name but differ in generic arity, so C# distinguishes them and compilation is clean, and the base list spells out the generic parameters so nothing is ambiguous.

**Namespaces do not repeat the module name.** `Finmy.Identity.Domain.Identity` and `Finmy.Identity.Infrastructure.Identity` repeated `Identity` twice and read badly. They are now split by what they contain: `RefreshTokens/` for `RefreshToken`, `Users/` for `ApplicationUser` and `ApplicationRole`.

**No file sits loose at a project root.** `ValidationFilter` used to live directly in `Finmy.Modularity`; it now lives in `Filters/`. Every file gets a folder that states its role.

## A sample module tree

```text
Finmy.Budgeting.Domain/
  Envelopes/            Envelope.cs, EnvelopeErrors.cs
  Categories/           Category.cs
Finmy.Budgeting.Application/
  Abstractions/         IEnvelopeRepository.cs, ICategoryRepository.cs
  Envelopes/            EnvelopeService.cs
    Dtos/               CreateEnvelopeRequest.cs, EnvelopeResponse.cs, CreateEnvelopeRequestValidator.cs
Finmy.Budgeting.Infrastructure/
  Persistence/          BudgetingDbContext.cs, BudgetingDbContextFactory.cs, EnvelopeRepository.cs, CategoryRepository.cs
  Migrations/
  DependencyInjection.cs
Finmy.Budgeting.Api/
  Endpoints/            EnvelopeEndpoints.cs
  BudgetingModule.cs
```

## Adding a new module

Follow the tree above. If you are about to use a name that is not in the table, change the table first, with the reasoning, and then use the name. That keeps this document a reference rather than an outdated description.
