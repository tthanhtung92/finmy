# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Language

Everything in the repo is written in **English**: code, docs, ADRs, commit messages. When talking to the repo owner in chat, reply in **Vietnamese and English side by side**. Commit history before 2026-08-04 is in Vietnamese and stays that way; it is not being rewritten.

## What this is

Finmy is a **Modular Monolith** shared-budgeting backend (envelope budgeting for any group) on **.NET 10 / C# 14**. It started as a technical showcase and is now being taken to production: self-hosted on a VPS with Docker and Kubernetes, with real CI/CD, observability, and quality gates.

The signature problem the project is built around is **anti-overspend on shared budget envelopes**: optimistic concurrency on the envelope balance, a Wolverine transactional outbox for atomic write-and-publish, and idempotent consumers. Depth belongs there, in the Ledger and Budgeting modules.

**What exists.** Identity: JWT auth with refresh-token rotation and revocation, `Result<T>`, FluentValidation, ProblemDetails, wired into the Bootstrap host through `AddModules()` / `UseModules()`. Budgeting: envelope CRUD with paginated list and monthly allocation summary, HybridCache cache-aside with tag invalidation (`BudgetingCachePolicy`), receipt upload to MinIO over the S3 API with magic-byte validation and presigned URLs, output caching plus Brotli/Gzip on report and list endpoints, SignalR realtime (`Hub<IEnvelopeClient>`, groups per envelope, the `IEnvelopeRealtimeNotifier` port), and a k6 benchmark in `bench/list-bench.js`. Ledger: the `Transaction` aggregate, an async `202 Accepted` endpoint, Wolverine in-process with a transactional outbox on schema `wolverine`, and `Idempotency-Key` support backed by `IIdempotencyStore` plus a `ProcessedTransaction` dedup table on the Budgeting side.

The anti-overspend loop is closed end to end: `Envelope` carries `Spent`, a computed `Remaining`, and `Spend` / `Release` / `Fund`; the concurrency token is a self-managed `int Version`; Budgeting consumes `TransactionPostedEvent`; insufficient funds raise `EnvelopeOverspentEvent` and Ledger reverses through `TransactionState`; `EnvelopeBalanceChangedEvent` drives cache invalidation, the SignalR push, and budget alerts, and Ledger's `TransactionConfirmedHandler` flips a transaction to `Confirmed` only once Budgeting has applied the deduction.

**Quality gates.** `Directory.Build.props` turns on the .NET analyzers at `AnalysisMode=Recommended` with `EnforceCodeStyleInBuild`, plus SonarAnalyzer.CSharp and Roslynator for every project; `TreatWarningsAsErrors` was already on, so all of it fails the build. `tests/Finmy.ArchitectureTests` holds the boundary rule with NetArchTest and the ADR-0009 `Version++` invariant with a Roslyn source guard. `tests/Finmy.IntegrationTests` drives the real host over HTTP through `FinmyApiFactory` against Postgres, Redis and MinIO containers. `scripts/coverage.ps1` keeps coverage from sliding below 52% lines and 48% branches.

**What does not exist yet**, so its absence is expected: no `Space` aggregate, no Dockerfile, no CI workflows, no observability, no rate limiting or API versioning. Budgeting and Ledger endpoints are still anonymous. The status store (`InMemoryTransactionStatusStore`) is still in memory. `docs/TECH-DEBT.md` is the authoritative list of known gaps; `docs/ROADMAP.md` and `README.md` are the authoritative spec.

Envelope balance stays in Budgeting by the single-writer rule ([ADR-0010](docs/adr/0010-single-writer-envelope-balance.md)).

## Commands

```bash
dotnet build Finmy.slnx                                 # build the whole solution
dotnet run --project src/Bootstrap/Finmy.Api            # run the host (composition root)

# tests — integration tests need Docker Desktop
dotnet test Finmy.slnx                                  # whole suite
dotnet test tests/Finmy.UnitTests/Finmy.UnitTests.csproj
dotnet test tests/Finmy.UnitTests/Finmy.UnitTests.csproj -- --filter-class "*EnvelopeSpendTests"

# coverage — runs the unit and integration suites, merges, fails below the floor
pwsh scripts/coverage.ps1
pwsh scripts/coverage.ps1 -SkipIntegration      # no Docker

# Wolverine/JasperFx diagnostics — the only verify tier that touches startup without a live DB
dotnet run --project src/Bootstrap/Finmy.Api -- describe
dotnet run --project src/Bootstrap/Finmy.Api -- wolverine-diagnostics describe-handlers RecordTransactionHandler
dotnet run --project src/Bootstrap/Finmy.Api -- codegen preview

# migrations — run from repo root; -s points at the host so the tool picks up its User Secrets
dotnet ef migrations list -p src/Modules/Ledger/Finmy.Ledger.Infrastructure -s src/Bootstrap/Finmy.Api
dotnet ef migrations add <Name> -p src/Modules/Ledger/Finmy.Ledger.Infrastructure -s src/Bootstrap/Finmy.Api
dotnet ef database update -p src/Modules/Ledger/Finmy.Ledger.Infrastructure -s src/Bootstrap/Finmy.Api
```

The test projects run on **Microsoft Testing Platform** (`xunit.v3.mtp-v2`, `OutputType=Exe`), not VSTest, and `global.json` opts `dotnet test` into it with `"test": { "runner": "Microsoft.Testing.Platform" }`.

That has one sharp edge. The old VSTest `--filter "FullyQualifiedName~X"` is still accepted, matches nothing, and reports **"Zero tests ran"** with exit code 5, which reads like a broken runner when the filter is what broke. MTP filters go after `--`: `--filter-class`, `--filter-method`, `--filter-query`. Plain `dotnet test` on the solution or on a single `.csproj` runs the whole suite and exits 0.

`describe` only reports at assembly level and does not list handlers; use `describe-handlers` for that.

`dotnet-ef` is pinned to `10.0.10` in `.config/dotnet-tools.json`, matching the EF runtime, alongside `dotnet-coverage` for the merge step. Run `dotnet tool restore` once after cloning; the local manifest takes precedence over any global install, so the version-skew warning does not come back.

A malformed `.runsettings` is reported as **"Zero tests ran"** with exit code 5 as well, with nothing said about parsing. The usual cause is a double hyphen inside an XML comment, which is illegal XML and easy to write when documenting the flags the file exists for.

Infrastructure (Postgres, Redis, MinIO) runs via `docker compose --env-file .env -f docker/docker-compose.yml up -d`. The `--env-file .env` is required: the compose files live in `docker/` but `.env` sits at the repo root, so without it every variable resolves empty. `docker/docker-compose.local.yml` is the same stack plus pgadmin and redisinsight.

## Architecture and boundaries

One process, source split into self-contained **modules** under `src/Modules/`: **Identity** (auth, JWT), **Budgeting** (envelopes, categories, receipts, cache, uploads), **Ledger** (transactions, outbox, anti-overspend concurrency). Each module is a vertical slice of four projects: `*.Domain` → `*.Application` → `*.Infrastructure` → `*.Api`.

- `src/Bootstrap/Finmy.Api` is the **only host and composition root**. It wires every module's services and endpoints through `AddModules()` / `UseModules()`.
- `src/Shared/Finmy.SharedKernel` holds `Result<T>`, domain-event base types, guards. `src/Shared/Finmy.Contracts` holds **integration events**, the only public cross-module surface. `src/Shared/Finmy.Modularity` holds the `IModule` abstraction, the `AddModules()` / `UseModules()` glue, `ResultExtensions` (mapping to ProblemDetails), and `ValidationFilter<T>`.

**The boundary rule, which is the point of the project:** a module must never reference another module's `Domain` or `Infrastructure` directly. Cross-module communication goes only through `Finmy.Contracts` integration events published over the Wolverine bus. `tests/Finmy.ArchitectureTests` enforces this, so adding a project reference between two modules turns the build red. Fix the design rather than the test.

## Stack and deliberate constraints

.NET 10, ASP.NET Core Minimal API, EF Core 10 with PostgreSQL 17, **Wolverine** (mediator, bus, transactional outbox), HybridCache (in-memory L1 plus Redis L2), SignalR, MinIO, ASP.NET Core Identity with JWT, FluentValidation, Mapster. Tests: xUnit v3 on Microsoft Testing Platform, NSubstitute, Shouldly, `Testcontainers.PostgreSql`.

**Licensing constraint.** This project deliberately avoids libraries that moved to commercial licensing in 2025: MediatR, AutoMapper, MassTransit, Moq, FluentAssertions. Do not introduce them. Use the chosen MIT/Apache/BSD replacements above (Wolverine for MediatR and MassTransit, Mapster or manual mapping for AutoMapper, NSubstitute for Moq, Shouldly for FluentAssertions).

### Wolverine traps that cost real time

- `WolverineFx.RuntimeCompilation` must stay referenced under `Condition="'$(Configuration)' == 'Debug'"` in `src/Bootstrap/Finmy.Api`. Wolverine 6.0 moved Roslyn out of the core package, so Dynamic codegen dies at **startup** with an `InvalidOperationException` about `IAssemblyGenerator` while `dotnet build` stays green.
- A handler's return value is a **cascading message**, not an ordinary return value: Wolverine publishes it onward. Use `InvokeAsync<T>` for request-reply (since v3.0 the reply type is not also published), or return plain `Task` to publish nothing.
- Handlers live outside the host assembly, so each module's `*.Application` needs `[assembly: WolverineModule]`. Without it Wolverine never scans that assembly and the message gets no handler.
- Any repository a handler injects must be `public`. Codegen emits `new EnvelopeRepository(...)` inside the generated `Internal.Generated.WolverineHandlers` assembly, so `internal` fails there while `dotnet build` stays green. Verify with `codegen preview`, not `build`.

### Two more

- No `JsonStringEnumConverter` is registered anywhere, so enums cross the wire as **numbers**. Posting `"Expense"` for `TransactionDirection` gets a 400; post `0`. Enum members carry explicit values for that reason, and reordering them is a wire break, not a rename.
- Every `IDesignTimeDbContextFactory` has to pass `MigrationsHistoryTable(HistoryRepository.DefaultTableName, "<schema>")` to `UseNpgsql`. Skip it and `__EFMigrationsHistory` lands in `public`, where all three modules collide.

### Concurrency token

The concurrency token is a plain `int Version` on `Envelope`, incremented by the domain in **every** mutating method and mapped with `IsConcurrencyToken()`. Deliberately **not** Postgres `xmin` / `IsRowVersion()`, which forces hand-editing a bogus `AddColumn` out of the migration and does not survive dump-restore ([ADR-0009](docs/adr/0009-self-managed-version-concurrency-token.md)). Forgetting an increment silently removes the protection, so a new mutating method needs both `Version++` and a test asserting it.

## Conventions

- **Commits:** Conventional Commits in English, imperative mood. For example `feat(auth): add refresh token rotation`, `docs: update README`.
- **Naming** (folders, files, namespaces) follows `docs/naming-conventions.md`, settled in ADR-0007: `Abstractions/` for interfaces, `{feature}/Dtos/`, singular service names, `{Module}DbContext`, plural feature folders, no stuttering namespaces. If a name does not fit the convention, fix the convention first, then the name.
- The solution uses the **`.slnx`** format (`Finmy.slnx`); edit it as XML when adding projects.
- `Directory.Build.props` and `Directory.Packages.props` (Central Package Management) live at the repo root. `Directory.Build.props` gives every project `net10.0`, `Nullable`, and `TreatWarningsAsErrors`; package versions are pinned centrally, so never put `Version=` on a `PackageReference`. Add shared build settings and central versions there.
- **Prose docs** (`README.md`, `docs/**`, ADRs): run the `humanizer:humanizer` skill before committing. Does not apply to commit messages or CLI code fences.
