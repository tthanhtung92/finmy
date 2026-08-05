# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Language

Everything in the repo is written in **English**: code, docs, ADRs, commit messages. When talking to the repo owner in chat, reply in **Vietnamese and English side by side**. Commit history before 2026-08-04 is in Vietnamese and stays that way; it is not being rewritten.

## What this is

Finmy is a **Modular Monolith** shared-budgeting backend (envelope budgeting for any group) on **.NET 10 / C# 14**. It started as a technical showcase and is now being taken to production: self-hosted on a VPS with Docker and Kubernetes, with real CI/CD, observability, and quality gates.

The signature problem the project is built around is **anti-overspend on shared budget envelopes**: optimistic concurrency on the envelope balance, a Wolverine transactional outbox for atomic write-and-publish, and idempotent consumers. Depth belongs there, in the Ledger and Budgeting modules.

**What exists.** Identity: JWT auth with refresh-token rotation and revocation, `Result<T>`, FluentValidation, ProblemDetails, wired into the Bootstrap host through `AddModules()` / `UseModules()`. Budgeting: envelope CRUD with paginated list and monthly allocation summary, HybridCache cache-aside with tag invalidation (`BudgetingCachePolicy`), receipt upload to MinIO over the S3 API with magic-byte validation and presigned URLs, output caching plus Brotli/Gzip on report and list endpoints varied by the caller's identity (`VaryByUserPolicy`), SignalR realtime (`Hub<IEnvelopeClient>`, groups per envelope, the `IEnvelopeRealtimeNotifier` port, authenticated over a query-string `access_token` since a WebSocket handshake cannot carry an `Authorization` header), and a k6 benchmark in `bench/list-bench.js`. Ledger: the `Transaction` aggregate, an async `202 Accepted` endpoint whose status resource answers `303 See Other` once it settles ([ADR-0015](docs/adr/0015-transaction-status-splits-to-sub-resource.md)), Wolverine in-process with a transactional outbox on schema `wolverine`, `Idempotency-Key` support backed by `IIdempotencyStore` plus a `ProcessedTransaction` dedup table on the Budgeting side, and a Postgres-backed transaction status store (`PostgresTransactionStatusStore`) with a background pruning service.

The anti-overspend loop is closed end to end: `Envelope` carries `Spent`, a computed `Remaining`, and `Spend` / `Release` / `Fund`; the concurrency token is a self-managed `int Version`; Budgeting consumes `TransactionPostedEvent`; insufficient funds raise `EnvelopeOverspentEvent` and Ledger reverses through `TransactionState`; `EnvelopeBalanceChangedEvent` drives cache invalidation, the SignalR push, and budget alerts, and Ledger's `TransactionConfirmedHandler` flips a transaction to `Confirmed` only once Budgeting has applied the deduction.

Observability: Serilog writes structured JSON to stdout, enriched with `trace_id`, `span_id` and `correlation_id` (`ActivityEnricher`, `CorrelationIdMiddleware`); OpenTelemetry instruments ASP.NET Core, HttpClient, Npgsql, Redis and Wolverine, exporting traces and metrics over OTLP once `OpenTelemetry:OtlpEndpoint` is configured; and `Finmy.SharedKernel.Observability.FinmyTelemetry` holds one `ActivitySource` (`Finmy.AntiOverspend`) and one `Meter` (`Finmy`) so a single transaction traces across both modules and business counters (`finmy.transactions.recorded`, `finmy.envelopes.overspent`, `finmy.envelope.concurrency_conflicts`) plus an outbox-backlog gauge feed one Grafana dashboard. `docker/docker-compose.observability.yml` brings up Prometheus, Tempo, Loki, Grafana and a collector on top of the base stack ([ADR-0017](docs/adr/0017-observability-shape.md)).

Every endpoint requires an authenticated user by default (`AddAuthorization`'s `FallbackPolicy`) except an explicit `AllowAnonymous` allowlist ([ADR-0016](docs/adr/0016-authenticated-by-default-with-anonymous-allowlist.md)); the whole module endpoint surface is served under `/api/v1`; `GET /health/live` and `/health/ready` exist, the latter probing all three Postgres schemas (including pending migrations), Redis and S3/MinIO; and rate limiting via `Microsoft.AspNetCore.RateLimiting` applies a global per-user/IP limit plus a tighter one on `/identity/register`, `/login` and `/refresh`.

Deployment: a Helm chart (`deploy/helm/finmy`) puts the API behind an Ingress with an HPA and PDB, alongside Postgres, Redis and MinIO running in-cluster on k3s's `local-path` storage class plus a nightly `pg_dump` CronJob; EF migrations and Wolverine's `resources setup` run from one plain, non-hook `Job` rather than a Helm lifecycle hook, since a hook either blocks the release before Postgres exists or never fires under `--wait`, and the API's own readiness probe is what actually gates traffic on that Job finishing. Production sets `JasperFx.Profile.ResourceAutoCreate = AutoCreate.None`, so the `wolverine` schema comes from that step rather than from auto-create at startup. Terraform (`deploy/terraform`) provisions a Hetzner VPS, firewall and DNS with k3s and cert-manager installed via cloud-init: written and `terraform validate`-clean, never applied. Secrets are SOPS/age-encrypted (`deploy/values-prod.sops.yaml`); CD is `release.yml`'s tag-triggered `deploy` job running `helm upgrade`, inert behind a `DEPLOY_ENABLED` repository variable. See [ADR-0018](docs/adr/0018-self-hosted-deployment-shape.md).

**Quality gates.** `Directory.Build.props` turns on the .NET analyzers at `AnalysisMode=Recommended` with `EnforceCodeStyleInBuild`, plus SonarAnalyzer.CSharp and Roslynator for every project; `TreatWarningsAsErrors` was already on, so all of it fails the build. `tests/Finmy.ArchitectureTests` holds the boundary rule with NetArchTest and the ADR-0009 `Version++` invariant with a Roslyn source guard. `tests/Finmy.IntegrationTests` drives the real host over HTTP through `FinmyApiFactory` against Postgres, Redis and MinIO containers; every Budgeting/Ledger HTTP test goes through `FinmyApiFactory.CreateAuthenticatedClientAsync()`, which logs in through the real `/api/v1/identity/login` endpoint rather than minting a JWT by hand. `scripts/coverage.ps1` keeps coverage from sliding below 60% lines and 54% branches.

**What does not exist yet**, so its absence is expected: no `Space` aggregate, no API versioning beyond the `/api/v1` route prefix (no `Asp.Versioning` library, since only one version exists), and no real Kubernetes cluster or applied Terraform: `deploy/` is written and validated (`helm lint`/`helm template`/`kubeconform`, `terraform validate`) but has never been deployed against a live VPS. `docs/TECH-DEBT.md` is the authoritative list of known gaps; `docs/ROADMAP.md` and `README.md` are the authoritative spec.

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

`.runsettings`' `ModulePaths` is an allowlist (`Finmy.*.dll` by file name), not a denylist. A third-party package's PDB can resolve on one OS and not another: `JasperFx` alone added 23000+ uncovered lines on the Linux CI runner the first time `coverage.ps1` ran there, invisible on the Windows dev machine. If coverage numbers look wrong on a new runner or OS, check this file before the test code.

`docker compose -f docker/docker-compose.yml up -d` brings up the whole system: Postgres, Redis, MinIO, a one-shot `migrate` service, then `api`. No `.env` needed, every value has an inline development default; `--env-file .env` only overrides them. `docker/docker-compose.local.yml` is a standalone duplicate (not an override layer) with pgadmin and redisinsight added, but it does not run `api`/`migrate` and its pgadmin now collides with `api` on host port 8080 (TECH-DEBT #17).

## Architecture and boundaries

One process, source split into self-contained **modules** under `src/Modules/`: **Identity** (auth, JWT), **Budgeting** (envelopes, categories, receipts, cache, uploads), **Ledger** (transactions, outbox, anti-overspend concurrency). Each module is a vertical slice of four projects: `*.Domain` → `*.Application` → `*.Infrastructure` → `*.Api`.

- `src/Bootstrap/Finmy.Api` is the **only host and composition root**. It wires every module's services and endpoints through `AddModules()` / `UseModules()`.
- `src/Shared/Finmy.SharedKernel` holds `Result<T>`, domain-event base types, guards. `src/Shared/Finmy.Contracts` holds **integration events**, the only public cross-module surface. `src/Shared/Finmy.Modularity` holds the `IModule` abstraction, the `AddModules()` / `UseModules()` glue, `ResultExtensions` (mapping to ProblemDetails), and `ValidationFilter<T>`.

**The boundary rule, which is the point of the project:** a module must never reference another module's `Domain` or `Infrastructure` directly. Cross-module communication goes only through `Finmy.Contracts` integration events published over the Wolverine bus. `tests/Finmy.ArchitectureTests` enforces this, so adding a project reference between two modules turns the build red. Fix the design rather than the test.

## Stack and deliberate constraints

.NET 10, ASP.NET Core Minimal API, EF Core 10 with PostgreSQL 17, **Wolverine** (mediator, bus, transactional outbox), HybridCache (in-memory L1 plus Redis L2), SignalR, MinIO, ASP.NET Core Identity with JWT, FluentValidation, Mapster, **Serilog** (structured logging), **OpenTelemetry** (traces and metrics, OTLP export). Tests: xUnit v3 on Microsoft Testing Platform, NSubstitute, Shouldly, `Testcontainers.PostgreSql`.

**Licensing constraint.** This project deliberately avoids libraries that moved to commercial licensing in 2025: MediatR, AutoMapper, MassTransit, Moq, FluentAssertions. Do not introduce them. Use the chosen MIT/Apache/BSD replacements above (Wolverine for MediatR and MassTransit, Mapster or manual mapping for AutoMapper, NSubstitute for Moq, Shouldly for FluentAssertions).

### Wolverine traps that cost real time

- Production codegen mode is `TypeLoadMode.Auto`, not `Static` ([ADR-0013](docs/adr/0013-wolverine-auto-codegen-in-production.md)), so `WolverineFx.RuntimeCompilation` is referenced **unconditionally** in `src/Bootstrap/Finmy.Api` on purpose. Do not add back a `Configuration == 'Debug'` condition. Wolverine 6.0 moved Roslyn out of the core package; without this reference, `Auto` mode's fallback throws `JasperFx.CodeGeneration.ExpectedTypeMissingException` on the **first handler invocation**, not at startup, so a health check alone will not catch it.
- A handler's return value is a **cascading message**, not an ordinary return value: Wolverine publishes it onward. Use `InvokeAsync<T>` for request-reply (since v3.0 the reply type is not also published), or return plain `Task` to publish nothing.
- Handlers live outside the host assembly, so each module's `*.Application` needs `[assembly: WolverineModule]`. Without it Wolverine never scans that assembly and the message gets no handler.
- Any repository a handler injects must be `public`. Codegen emits `new EnvelopeRepository(...)` inside the generated `Internal.Generated.WolverineHandlers` assembly, so `internal` fails there while `dotnet build` stays green. Verify with `codegen preview`, not `build`.

### Two more

- No `JsonStringEnumConverter` is registered anywhere, so enums cross the wire as **numbers**. Posting `"Expense"` for `TransactionDirection` gets a 400; post `0`. Enum members carry explicit values for that reason, and reordering them is a wire break, not a rename.
- Every `IDesignTimeDbContextFactory` has to pass `MigrationsHistoryTable(HistoryRepository.DefaultTableName, "<schema>")` to `UseNpgsql`. Skip it and `__EFMigrationsHistory` lands in `public`, where all three modules collide.

### Observability

- Every exporter (Serilog's OTLP sink, the OpenTelemetry trace and metric exporters) is gated on `OpenTelemetry:OtlpEndpoint` being non-empty. Leave it blank and the host still wires in-process instrumentation, just with nowhere to send it, so a bare `dotnet run` or the test suite never spends a flush interval failing to connect to a collector that is not running.
- `Program.cs` builds one `IConnectionMultiplexer` explicitly (`ConnectionMultiplexer.ConnectAsync`, `AbortOnConnectFail = false`) and registers it as a singleton, so `AddStackExchangeRedisCache`'s `ConnectionMultiplexerFactory` and the Redis trace instrumentation share the same connection instead of each opening its own. A new consumer of Redis should resolve `IConnectionMultiplexer` from DI rather than parsing the connection string again.
- `OutboxBacklogMetrics`'s `SqlQueryRaw<long>` needs its column aliased `as "Value"` (`select count(*) as "Value" from ...`). EF Core's scalar mapping for `SqlQueryRaw<T>` always looks for a column literally named `Value`; Postgres names an unaliased `count(*)` `count`, so leaving off the alias throws at query time, not at compile time.

### Concurrency token

The concurrency token is a plain `int Version` on `Envelope`, incremented by the domain in **every** mutating method and mapped with `IsConcurrencyToken()`. Deliberately **not** Postgres `xmin` / `IsRowVersion()`, which forces hand-editing a bogus `AddColumn` out of the migration and does not survive dump-restore ([ADR-0009](docs/adr/0009-self-managed-version-concurrency-token.md)). Forgetting an increment silently removes the protection, so a new mutating method needs both `Version++` and a test asserting it.

## Conventions

- **Commits:** Conventional Commits in English, imperative mood. For example `feat(auth): add refresh token rotation`, `docs: update README`.
- **Naming** (folders, files, namespaces) follows `docs/naming-conventions.md`, settled in ADR-0007: `Abstractions/` for interfaces, `{feature}/Dtos/`, singular service names, `{Module}DbContext`, plural feature folders, no stuttering namespaces. If a name does not fit the convention, fix the convention first, then the name.
- The solution uses the **`.slnx`** format (`Finmy.slnx`); edit it as XML when adding projects.
- `Directory.Build.props` and `Directory.Packages.props` (Central Package Management) live at the repo root. `Directory.Build.props` gives every project `net10.0`, `Nullable`, and `TreatWarningsAsErrors`; package versions are pinned centrally, so never put `Version=` on a `PackageReference`. Add shared build settings and central versions there.
- **Prose docs** (`README.md`, `docs/**`, ADRs): run the `humanizer:humanizer` skill before committing. Does not apply to commit messages or CLI code fences.
- `main`'s branch protection is a modern **Ruleset** (`gh api repos/tthanhtung92/finmy/rulesets`), not classic branch protection. It predates Phase 2 and already required PR review; Phase 2 only added required status checks to it. Extend it in place (`PUT .../rulesets/<id>` with the full rule set), don't create a second, conflicting classic-protection config.
