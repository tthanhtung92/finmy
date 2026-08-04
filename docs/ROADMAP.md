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

**Phase 3: production hardening.** Real health checks with separate liveness and readiness endpoints probing Postgres, Redis and S3. Close the authorization gap on Budgeting and Ledger endpoints with a global authenticated-user fallback policy. Rate limiting. API versioning under `/api/v1`. Production configuration reading secrets from the environment, failing fast when a connection string is missing. A migration strategy that does not run `Database.Migrate()` at startup with multiple replicas. Move the transaction status store out of memory. Resilience policies on outbound calls.

**Phase 4: observability.** Serilog writing structured JSON to stdout, enriched with correlation and trace IDs. Full OpenTelemetry: traces across ASP.NET Core, HttpClient, Npgsql, Redis and Wolverine; runtime and business metrics; OTLP export. A self-hosted collector into Prometheus, Tempo, Loki and Grafana. A dedicated `ActivitySource` for the anti-overspend path so one transaction can be traced across both modules. Dashboards and alerts on error rate, p95 latency, outbox backlog and concurrency conflicts.

**Phase 5: self-hosted deployment.** Helm chart with Deployment, Service, Ingress, HPA, PDB and probes. k3s on a VPS with an ingress controller and cert-manager for TLS. Terraform for the VPS, DNS and firewall. Encrypted secrets through Sealed Secrets or SOPS. A decision on whether Postgres, Redis and MinIO run in-cluster with backups or move to managed services. Continuous deployment through Actions on tag, or Argo CD.

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
