# Finmy: Modular Monolith Backend (.NET 10)

> A shared-budgeting backend for a group, built on envelope budgeting. Each core backend concern (authentication, realtime, caching, CDN, messaging, concurrency, operations) gets one thin slice that actually runs.

**License:** MIT · **Architecture:** Modular Monolith · **Platform:** .NET 10 LTS

---

## 1. Goals

This project does not chase feature count. Each backend concept gets a vertical slice that is thin but real, tested, and explainable. A README that explains its architectural decisions well is worth more than fifty CRUD endpoints.

The domain is shared budgeting because the author self-hosts it for an actual group, which is what keeps it honest: the system has a user, so it has to keep working. The full reasoning is in [ADR-0006](adr/0006-pivot-to-shared-budgeting.md).

Three principles:

1. **Correct over broad.** Anything marked done runs for real, not as a mockup.
2. **Explainable.** Every technical choice has a reason recorded in the README or an ADR.
3. **`docker compose up` and it runs.** Cloning and starting the system takes one command.

As of 2026-08-04 the project is being taken from technical showcase to a production deployment: self-hosted on a VPS with Docker and Kubernetes, with CI/CD, observability and quality gates. Section 5 is the plan for that.

---

## 2. Tech stack

| Layer | Choice | License | Why |
|-------|--------|---------|-----|
| Runtime | **.NET 10 LTS** | MIT | Supported until 11/2028 |
| Web | **ASP.NET Core 10** (Minimal API) | MIT | Built-in OpenAPI 3.1, auth metrics |
| Language | **C# 14** | MIT | `field` keyword, primary constructors |
| ORM | **EF Core 10** | MIT | Optimistic concurrency, named query filters |
| Database | **PostgreSQL 17** | OSS | Capable, free, widely deployed |
| Messaging | **Wolverine** | MIT | Mediator, message bus and outbox in one package |
| Mapping | **Mapster** or manual | MIT | AutoMapper went commercial |
| Validation | **FluentValidation** | Apache 2.0 | Still free |
| Cache | **HybridCache** (L1 in-memory, L2 Redis) | MIT | Stable in .NET 10 |
| Cache store | **Redis 8** | OSS | Distributed cache plus pub/sub |
| Realtime | **SignalR** | MIT | Built in, no licensing issue |
| Object storage | **MinIO** (S3-compatible) | AGPL | Local CDN origin for receipt images |
| Auth | **ASP.NET Core Identity + JWT** | MIT | Standard, refresh tokens, role-based |
| Logging | **Serilog** | Apache 2.0 | Structured logging |
| Tracing | **OpenTelemetry** | Apache 2.0 | Vendor-neutral traces and metrics |
| Tests | **xUnit v3, NSubstitute, Shouldly, Testcontainers** | MIT/BSD | Avoids Moq and FluentAssertions, both commercial |
| CI/CD | **GitHub Actions** | n/a | Build, test, push image |
| Container | **Docker multi-stage + Compose** | n/a | Reproducible environment |
| Orchestration | **k3s + Helm** | Apache 2.0 | Self-hosted, right-sized for one product |

Two constraints worth stating explicitly:

**The 2025 licensing wave.** MediatR, AutoMapper, MassTransit, FluentAssertions and Moq all moved to commercial licenses. This project picked MIT-licensed replacements deliberately; see [ADR-0003](adr/0003-avoid-commercial-libraries.md).

**Wolverine codegen mode.** Development uses Dynamic, which compiles handlers with Roslyn at startup. Production uses Auto, which loads pre-built handler types when they exist on disk and falls back to generating them the same way Dynamic does otherwise; see [ADR-0013](adr/0013-wolverine-auto-codegen-in-production.md) for why Static was tried first and rejected.

**Money** is stored as `decimal`, with rounding handled explicitly.

---

## 3. Solution structure

Each **module** is a self-contained vertical slice (Domain, Application, Infrastructure, API endpoints). Modules talk to each other only through the message bus or public contracts, never by referencing each other's internals. That constraint is what makes it modular rather than a tangled monolith.

The root aggregate for sharing is **Space**: it will own Account, Category, Envelope and Transaction, and it is the authorization boundary, so a user only touches data belonging to their own Space. Members join a Space through **Member** with an Owner, Member or Viewer role. A Space is a flat sharing group, not tied to any particular kind of group.

```text
finmy/
├── .github/workflows/                  # CI: build, test, push image
├── docker/
│   ├── docker-compose.yml              # postgres + redis + minio (+ api)
│   └── docker-compose.local.yml        # same plus pgadmin, redisinsight
├── src/
│   ├── Bootstrap/
│   │   └── Finmy.Api/                  # the only host, composition root
│   │       ├── Program.cs
│   │       ├── Extensions/             # AddModules(), UseModules()
│   │       ├── Middleware/             # GlobalExceptionHandler
│   │       └── appsettings.json
│   ├── Modules/
│   │   ├── Identity/
│   │   │   ├── Finmy.Identity.Domain/          # RefreshToken (POCO, UserId reference)
│   │   │   ├── Finmy.Identity.Application/     # login/register handlers, IIdentityService
│   │   │   ├── Finmy.Identity.Infrastructure/  # ApplicationUser/Role, DbContext, JWT
│   │   │   └── Finmy.Identity.Api/
│   │   ├── Budgeting/
│   │   │   ├── Finmy.Budgeting.Domain/         # Envelope, Category, Receipt (Space, Member planned)
│   │   │   ├── Finmy.Budgeting.Application/    # CRUD, cache, handlers
│   │   │   ├── Finmy.Budgeting.Infrastructure/ # EF, Redis, MinIO
│   │   │   └── Finmy.Budgeting.Api/
│   │   └── Ledger/
│   │       ├── Finmy.Ledger.Domain/            # Transaction
│   │       ├── Finmy.Ledger.Application/       # Wolverine handlers, outbox
│   │       ├── Finmy.Ledger.Infrastructure/    # EF, concurrency, idempotency store
│   │       └── Finmy.Ledger.Api/
│   └── Shared/
│       ├── Finmy.SharedKernel/         # Result<T>, DomainEvent base, guards
│       ├── Finmy.Modularity/           # IModule, AddModules()/UseModules(), ResultExtensions
│       └── Finmy.Contracts/            # integration events, the public cross-module surface
├── tests/
│   ├── Finmy.UnitTests/                # domain logic, handlers (NSubstitute)
│   ├── Finmy.IntegrationTests/         # Testcontainers: real Postgres
│   └── Finmy.ArchitectureTests/        # NetArchTest: enforces module boundaries
├── docs/
│   ├── ROADMAP.md
│   ├── TECH-DEBT.md
│   ├── naming-conventions.md
│   └── adr/                            # Architecture Decision Records
├── .editorconfig
├── Directory.Build.props
├── Directory.Packages.props            # Central Package Management
├── Finmy.slnx
├── CLAUDE.md
├── LICENSE
└── README.md
```

Folder, file, namespace and class naming follows [naming-conventions.md](naming-conventions.md), settled in ADR-0007.

### The module boundary rule

- A module must not reference another module's `Domain` or `Infrastructure` directly.
- Cross-module communication goes only through `Finmy.Contracts` integration events published over Wolverine.
- `Finmy.ArchitectureTests` uses **NetArchTest** to fail the build when someone breaks the rule.

---

## 4. Concept to implementation map

| Concept | Module | Implementation |
|---------|--------|----------------|
| **Authentication** | Identity | JWT plus refresh-token rotation, role-based authorization |
| **CRUD and database** | Budgeting | EF Core 10, envelopes and categories, pagination, validation |
| **Caching** | Budgeting | HybridCache cache-aside for balances and reports, tag invalidation on write |
| **CDN** | Budgeting | Receipt upload to MinIO, served through a cache layer with presigned URLs |
| **Realtime** | Budgeting | SignalR pushes new envelope balances to everyone watching |
| **Messaging** | Ledger | Wolverine: async transaction recording plus transactional outbox |
| **Concurrency** | Ledger + Budgeting | Optimistic concurrency on the envelope balance, compensating reversal on overspend |
| **Idempotency** | Ledger | `Idempotency-Key` on writes, dedup table on the consumer, CSV import dedup (planned) |
| **Observability** | whole system | Serilog structured logs plus OpenTelemetry traces and metrics |
| **Operations** | whole system | Docker multi-stage, GitHub Actions, Helm on k3s |

---

## 5. Roadmap

Phases run in order. Each is a separate implementation plan; open items carried between them are listed in [TECH-DEBT.md](TECH-DEBT.md).

**Phase 0: repository cleanup (done 2026-08-04).** Removed the tutorial layer the project was built with, switched everything to English, and made `CLAUDE.md` a tracked file. Extracted the decisions and deferred work that only existed in local notes into [ADR-0010](adr/0010-single-writer-envelope-balance.md), [ADR-0011](adr/0011-async-request-reply-202.md) and [TECH-DEBT.md](TECH-DEBT.md).

**Phase 1: build and quality gates (done 2026-08-04).** Pinned the SDK to 10.0.302 in `global.json` and moved the tool manifest under `.config/`. Turned on .NET analyzers at `Recommended` plus SonarAnalyzer and Roslynator, cleared the 101 warnings they raised, and gave `.editorconfig` real severities. Added `Finmy.ArchitectureTests`: NetArchTest for the boundary rule and the banned libraries of [ADR-0003](adr/0003-avoid-commercial-libraries.md), plus a Roslyn guard that every mutating `Envelope` method still bumps `Version` as [ADR-0009](adr/0009-self-managed-version-concurrency-token.md) requires. Added coverage through the MTP collector with `scripts/coverage.ps1` holding the floor at 52% lines and 48% branches. Paid off TECH-DEBT #5 and #6: `WebApplicationFactory` now drives the anti-overspend loop over HTTP against Postgres, Redis and MinIO containers, and a concurrent envelope write answers 409 instead of 500.

**Phase 2: packaging and CI/CD (done 2026-08-04).** Multi-stage Dockerfile with a non-root user, plus `.dockerignore`. An `api` service and a one-shot `migrate` service in the compose file, so `docker compose up` needs no `.env` and no manual migration step. GitHub Actions for build, test, architecture test, integration test and coverage, and a release workflow pushing images to GHCR, verified against a real tag. Dependabot, CODEOWNERS, CodeQL, and `dotnet list package --vulnerable` as a gate. Branch protection on `main` through the existing ruleset, extended with required status checks rather than replaced.

**Phase 3: production hardening (done 2026-08-05).** `GET /health/live` and `/health/ready`, the latter probing all three Postgres schemas (including pending migrations), Redis and S3/MinIO. `AddAuthorization`'s `FallbackPolicy` now requires an authenticated user everywhere except an explicit `AllowAnonymous` allowlist ([ADR-0016](adr/0016-authenticated-by-default-with-anonymous-allowlist.md)), which also fixed a dormant bug: every module's runtime `AddDbContext` was missing the schema-qualified `MigrationsHistoryTable` its design-time factory already had. Output caching stayed on by varying its cache key on the caller's `sub` claim instead of going dark once every request carries a bearer token. Rate limiting via `Microsoft.AspNetCore.RateLimiting`, a global per-user/IP limit plus a tighter one on login/register/refresh. The whole module endpoint surface moved under `/api/v1`. `GetRequiredConnectionString` replaced five near-identical fail-fast blocks with one. The transaction status store moved to Postgres ([`PostgresTransactionStatusStore`](../src/Modules/Ledger/Finmy.Ledger.Infrastructure/Persistence/PostgresTransactionStatusStore.cs)), with a route split so `GET /transactions/{id}/status` answers `303 See Other` once `GET /transactions/{id}` has something to point at ([ADR-0015](adr/0015-transaction-status-splits-to-sub-resource.md)), and a pruning background service sweeping rows past their retention window. [ADR-0014](adr/0014-migration-strategy-for-multiple-replicas.md) confirmed EF Core's own migration lock already serializes concurrent migrators, so no extra locking was added; a readiness-gate check on pending migrations is what actually closes the rolling-deploy risk. The S3 client got explicit retry and timeout configuration, and `BucketInitializer` a bounded retry loop instead of a bare first-attempt failure.

**Phase 4: observability (done 2026-08-05).** Serilog writing structured JSON to stdout, enriched with trace and correlation IDs through a `CorrelationIdMiddleware` and `ActivityEnricher` pair. Full OpenTelemetry: traces across ASP.NET Core, HttpClient, Npgsql, Redis and Wolverine; runtime and business metrics; OTLP export gated on configuration so a bare `dotnet run` or the test suite needs no collector. A self-hosted collector into Prometheus, Tempo, Loki and Grafana, brought up alongside the base stack with `docker/docker-compose.observability.yml`. A dedicated `ActivitySource` (`Finmy.AntiOverspend`, in `Finmy.SharedKernel` so neither module reaches into the other) traces one transaction across both Ledger and Budgeting; confirmed against a live collector, where a single overspending request produced one Tempo trace containing both modules' spans and the underlying Npgsql client spans. A provisioned Grafana dashboard and four alert rules cover error rate, p95 latency, outbox backlog and concurrency conflicts ([ADR-0017](adr/0017-observability-shape.md)).

**Phase 5: self-hosted deployment (done 2026-08-05).** A Helm chart (`deploy/helm/finmy`) with the API's Deployment, Service, Ingress, HPA and PDB wired to `/health/live` and `/health/ready`, plus Postgres, Redis and MinIO as in-cluster StatefulSets/Deployment on k3s's `local-path` storage class with a nightly `pg_dump` CronJob for backup ([ADR-0018](adr/0018-self-hosted-deployment-shape.md)). EF migrations and Wolverine's `resources setup` run from one plain, non-hook Job rather than a Helm lifecycle hook, since a hook either blocks the release before Postgres exists or never fires under `--wait`; the API's readiness probe (already checking `GetPendingMigrationsAsync` per ADR-0014) is what actually gates traffic on that Job finishing. Production sets `JasperFx.Profile.ResourceAutoCreate = AutoCreate.None`, closing TECH-DEBT #4. Terraform (`deploy/terraform`) provisions a Hetzner VPS, firewall and DNS, with k3s and cert-manager installed through cloud-init; written and `terraform validate`-clean against real provider schemas, but never applied, so no server exists yet and "deployed and reachable over HTTPS" stays open in section 6. Secrets are encrypted with SOPS and age (`deploy/values-prod.sops.yaml`), closing TECH-DEBT #15; the full decrypt-and-render pipeline was verified end to end with real `sops`, `age` and `helm` binaries. Continuous deployment is `release.yml`'s tag-triggered `deploy` job running `helm upgrade`, inert behind a `DEPLOY_ENABLED` repository variable until a cluster exists to point it at.

**Phase 6: Space, membership and authorization.** The `Space` aggregate, membership with Owner, Member and Viewer roles, and an invitation flow. Envelopes, accounts and transactions bound to a `SpaceId`, with resource-based authorization on membership rather than JWT roles alone, and query filters so data cannot leak between groups.

**Phase 7: scale and distribution, when there is a real need.** Scheduled background jobs for monthly reports and expired-token cleanup. Feature flags to separate deploy from release. CSV statement import with deduplication. Moving the broker out of process is worth considering only once modules split into separate services; Wolverine with a Postgres outbox is serving well today. gRPC, GraphQL and Aspire are deliberately out of scope.

---

## 6. Definition of done

- [x] `git clone` then `docker compose up` brings the whole system up with no manual steps.
- [x] All three modules work end to end.
- [ ] Every concept in section 4 has a slice that actually runs.
- [x] Unit, integration and architecture tests green in CI.
- [ ] The overspend scenario is demonstrable.
- [ ] README carries an architecture diagram, benchmark numbers and a realtime demo.
- [ ] Deployed and reachable over HTTPS, with dashboards showing it is healthy.
- [ ] MIT licensed, repository public.
