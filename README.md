# Finmy

> A shared-budgeting backend for a group, built on **envelope budgeting**, structured as a **Modular Monolith** on **.NET 10**. Every core backend concern (authentication, realtime, caching, CDN, messaging, concurrency) gets one slice that actually runs, kept minimal but done properly.

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](./LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)

---

## Contents

- [Overview](#overview)
- [Current state](#current-state)
- [What is demonstrated](#what-is-demonstrated)
- [Architecture](#architecture)
- [Transaction write path](#transaction-write-path)
- [Tech stack](#tech-stack)
- [Quick start](#quick-start)
- [Project layout](#project-layout)
- [Testing](#testing)
- [Cache benchmark](#cache-benchmark)
- [Architecture decisions](#architecture-decisions)
- [Roadmap](#roadmap)
- [License](#license)

---

## Overview

Finmy lets a group manage shared money the envelope way: income is divided into budget envelopes per spending category (food, utilities, tuition), and each transaction draws down the matching envelope. Several members of a Space see and spend against the same set of budgets, with Owner, Member and Viewer roles.

The hard problem is **several people spending from the same nearly-empty envelope at once**: two concurrent transactions must not push the balance below what is left. That part works. The solution is optimistic concurrency on the envelope balance, plus Wolverine's transactional outbox so recording a transaction and publishing its event happen in one transaction. The concurrency token is an `int Version` column the domain increments in every mutating method, mapped with `IsConcurrencyToken()`, deliberately not Postgres `xmin` (reasoning under [Architecture](#architecture)). An integration test runs the two-concurrent-transactions scenario against a real Postgres via Testcontainers.

The repository previously modelled event ticketing; the reason for moving to shared budgeting is in [ADR-0006](docs/adr/0006-pivot-to-shared-budgeting.md).

---

## Current state

A personal project under active construction, on its way to a real deployment but not there yet. This section separates what runs from what is planned so nobody has to guess.

**Built and running:**

- Modular Monolith skeleton: `Finmy.Api` as the composition root, `IModule` for self-registration, one DbContext per module on its own schema (`identity`, `budgeting`, `ledger`, plus `wolverine` for the message store).
- **Identity**, all four layers: registration, login, JWT access tokens. Refresh-token rotation, with reuse of a revoked token revoking the user's entire chain. Tokens are generated from an RNG and stored as SHA-256 hashes behind a unique index on `TokenHash`. Admin and User roles plus a default admin seeded through an `IHostedService`, credentials read from configuration. Endpoints under `/identity`: `/register`, `/login`, `/refresh`, `/logout`, `/me`, `/admin-only`. The module has been through a security review with the findings fixed.
- **Budgeting**: full envelope CRUD (create, read, paginated list, update, delete) and a monthly allocation report, with categories seeded in a migration.
  - **Caching**: HybridCache cache-aside for the envelope list and the monthly report with per-entry TTLs, tag-based invalidation on writes (`BudgetingCachePolicy` plus `RemoveByTagAsync`), and output caching with Brotli/Gzip compression on the two read-heavy endpoints, evicted through the `IOutputCacheInvalidator` port.
  - **CDN and object storage**: receipt upload to MinIO over the S3 API (AWSSDK.S3), validated by magic bytes, with a server-generated object key and a `Receipt` pointer row in Postgres. `POST /receipts` uploads; `GET /receipts/{id}` answers 302 with a presigned URL and `Cache-Control`.
  - **Realtime**: a strongly-typed `Hub<IEnvelopeClient>` at `/hubs/envelopes`, one group per envelope, pushing `EnvelopeUpdated`, `EnvelopeAlert` and `EnvelopeDeleted`. The Application layer only knows the `IEnvelopeRealtimeNotifier` port, not SignalR.
  - **Balance and overspend protection**: `Envelope` holds `Spent`, computes `Remaining` from `Allocated - Spent`, and mutates through `Spend`, `Release` and `Fund`. Spending past the balance returns a domain error rather than going negative.
- **Ledger**: the `Transaction` aggregate with `TransactionState` (`Posted`, `Reversed`, `Confirmed`), `POST /transactions` answering **202 Accepted** and processing asynchronously, and `GET /transactions/{id}` for status.
  - **Messaging and outbox**: Wolverine in-process, Dynamic codegen in development and Static elsewhere, message store on the `wolverine` schema, `AddDbContextWithWolverineIntegration` so writing a `Transaction` and enqueuing its message share one transaction. `DbUpdateConcurrencyException` gets its own retry-with-cooldown policy before the message moves to the error queue.
  - **Idempotency**: `Idempotency-Key` on `POST /transactions` backed by `IIdempotencyStore`, with a request fingerprint so a reused key carrying a different payload is rejected with 422 rather than silently replayed. On the consumer side, a `ProcessedTransaction` table makes the Budgeting handler idempotent, so a redelivered message does not deduct twice.
- **Integration events** in `Finmy.Contracts`: `TransactionPostedEvent`, `EnvelopeOverspentEvent`, `EnvelopeBalanceChangedEvent`. The full chain is described under [Transaction write path](#transaction-write-path).
- **Tests**: unit tests for the Envelope domain (create, update, spend, fund), `EnvelopeService`, cache policy, alert policy, the receipt validator and the Transaction domain; an integration test for the concurrent-spend race running against real Postgres through Testcontainers.
- `Result<T>`, `Error` and `ErrorType` in SharedKernel, a `GlobalExceptionHandler` returning ProblemDetails without leaking stack traces, and `ValidationFilter<T>` with FluentValidation rejecting bad input at the endpoint.
- OpenAPI plus Scalar UI in Development.
- Docker Compose for the dependencies: PostgreSQL 17, Redis 8, MinIO.
- Eleven ADRs plus `docs/naming-conventions.md`, and `docs/TECH-DEBT.md` listing known gaps.

**Not built yet:** Space, Account, Member and per-Space authorization; CSV statement import with deduplication; a durable transaction status store (still in memory, so restarting loses it); Serilog and OpenTelemetry; NetArchTest architecture tests; a Dockerfile for the API; CI on GitHub Actions; rate limiting and API versioning.

Endpoints in Budgeting and Ledger are currently unauthenticated. That is a known gap, tracked as item 1 in [TECH-DEBT.md](docs/TECH-DEBT.md), and it is fixed before any public deployment.

---

## What is demonstrated

| Concern | Module | How | Status |
| --- | --- | --- | --- |
| **Authentication** | Identity | JWT plus refresh-token rotation, role-based authorization | Done |
| **Error handling** | whole system | `Result<T>` plus ProblemDetails plus FluentValidation | Done |
| **CRUD and database** | Budgeting | EF Core 10: envelope CRUD and monthly report, pagination, validation | In progress |
| **Caching** | Budgeting | HybridCache (L1 in-memory, L2 Redis), cache-aside with tag invalidation, output caching and compression | Done |
| **CDN / object storage** | Budgeting | Receipt upload to MinIO (S3 API), served through a cache layer with presigned URLs | Done |
| **Realtime** | Budgeting | SignalR pushes new balances and alerts to watching clients | Done |
| **Messaging** | Ledger | Wolverine in-process: async transaction recording plus transactional outbox | Done |
| **Concurrency** | Ledger + Budgeting | Optimistic concurrency on `Envelope.Version`, compensating reversal on overspend | Done |
| **Idempotency** | Ledger | `Idempotency-Key` with request fingerprint, plus a consumer dedup table | Done |
| **Observability** | whole system | Serilog structured logging plus OpenTelemetry tracing | Planned |
| **Operations** | whole system | Docker multi-stage, GitHub Actions, Helm on k3s | Planned |

---

## Architecture

Finmy is a **Modular Monolith**: one process, source split into independent modules. Each module contains its own Domain, Application, Infrastructure and API endpoints, and modules communicate **only through integration events** in `Finmy.Contracts`, never by referencing each other's internals.

All three modules have working code. Space is the root aggregate for sharing: it will own Account, Category, Envelope and Transaction, and it is the authorization boundary. Today `SpaceId` exists only as a column on Transaction; the aggregate itself is not written.

```text
┌─────────────────────────────────────────────┐
│              Finmy.Api (host)                │
│              composition root                │
├───────────┬───────────────┬─────────────────┤
│  Identity │   Budgeting    │     Ledger      │
│  (done)   │  (in progress) │  (in progress)  │
│           │ Envelope/       │ Transaction     │
│           │ Category/Receipt│ (outbox)        │
└───────────┴───────────────┴─────────────────┘
        │            │              │
        └──── Wolverine message bus ┘
              (integration events)
                     │
   ┌─────────┬───────┴────────┬──────────┐
PostgreSQL   Redis           MinIO     SignalR
```

The envelope balance is written only by Budgeting. Ledger never touches the envelope table; it publishes an event and waits for the answer. This single-writer rule is why overspend protection lives in Budgeting rather than Ledger, even though the business rule sounds like Ledger's job. The full reasoning, including what eventual consistency costs here, is in [ADR-0010](docs/adr/0010-single-writer-envelope-balance.md).

The concurrency token is a self-managed `int Version` rather than `xmin` through `IsRowVersion()`. `xmin` forces hand-editing a meaningless `AddColumn` out of every migration, and its value does not survive a dump and restore. The self-managed version has its own cost: forgetting `Version++` in a new mutating method silently removes the protection while the build stays green, so every mutating method needs a test asserting the version increments.

Module boundaries are enforced by NetArchTest in `tests/Finmy.ArchitectureTests`, alongside a Roslyn guard that fails the build when a mutating `Envelope` method forgets to bump `Version`.

Reasoning behind the choices is in the [ADRs](docs/adr/).

---

## Transaction write path

This slice touches most of the difficult parts of the repository, so it is written out here to read alongside the code.

1. The client calls `POST /transactions`, optionally with an `Idempotency-Key`. A repeated key returns the original outcome instead of creating a second transaction; a repeated key with a different payload is rejected with 422. The endpoint generates a v7 `Guid`, marks the request pending and answers **202 Accepted** with a status URL.
2. `RecordTransactionHandler` writes the `Transaction` in state `Posted` and enqueues `TransactionPostedEvent` in the same transaction, through Wolverine's outbox. If the write fails, the event never leaves, so there is no case where a transaction disappears but its event was published.
3. Budgeting receives the event in `TransactionPostedHandler`, which records the transaction id in a dedup table so a redelivery is a no-op. Expenses call `Envelope.Spend`; income calls `Envelope.Fund` to add budget. The two are deliberately separate: `Fund` adds to `Allocated`, while `Release` (a refund) reduces `Spent`.
4. If the balance is short, Budgeting publishes `EnvelopeOverspentEvent`. Ledger handles it in `EnvelopeOverspentHandler` and reverses the transaction to `Reversed`, while Budgeting's `EnvelopeOverspentAlertHandler` pushes an alert to the client.
5. If the deduction succeeds, Budgeting publishes `EnvelopeBalanceChangedEvent`. Two Budgeting handlers consume it: one evicts cache by tag, the other pushes the new balance over SignalR and adds an alert when the balance drops below 20% of the allocation (`BudgetingAlertPolicy`). Because of `MultipleHandlerBehavior.Separated`, each handler runs in its own chain, so one failing does not take the other down.
6. Ledger listens to that same event in `TransactionConfirmedHandler` and only then flips the transaction to `Confirmed`. `Confirmed` therefore means the money was actually deducted in Budgeting, not merely that the row was written.

When two transactions run concurrently against a nearly-empty envelope, the loser gets a `DbUpdateConcurrencyException`. Wolverine retries three times with a cooldown, re-reading the current balance each attempt, and moves the message to the error queue once retries are exhausted.

---

## Tech stack

In use today:

| Layer | Technology |
| --- | --- |
| Runtime | .NET 10, C# 14 |
| Web | ASP.NET Core 10 (Minimal API) |
| ORM / database | EF Core 10, PostgreSQL 17 (Npgsql) |
| Auth | ASP.NET Core Identity plus JWT Bearer |
| Messaging | Wolverine 6 (mediator, bus, transactional outbox) |
| Realtime | SignalR |
| Validation | FluentValidation |
| Caching | HybridCache (L1 in-memory, L2 Redis), output caching |
| Object storage | MinIO through AWSSDK.S3 |
| API docs | OpenAPI plus Scalar (Development only) |
| Tests | xUnit v3 on Microsoft Testing Platform, NSubstitute, Shouldly, Testcontainers |
| Infrastructure | Docker Compose (PostgreSQL, Redis, MinIO) |

Quality gates: .NET analyzers, SonarAnalyzer, Roslynator, NetArchTest, and code coverage through the Microsoft Testing Platform collector. Planned: Mapster, Serilog, OpenTelemetry, GitHub Actions, Helm on k3s.

> **On licensing:** the project deliberately avoids libraries that moved to commercial licenses in 2025 (MediatR, AutoMapper, MassTransit, Moq, FluentAssertions) and uses equivalent replacements. Details in [ADR-0003](docs/adr/0003-avoid-commercial-libraries.md).

> **On money:** amounts are stored as `decimal` with rounding handled explicitly. Automatic import from Vietnamese banks is not practical without widespread open banking, so input comes from CSV or statement upload, or manual entry.

---

## Quick start

### Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker](https://www.docker.com/) and Docker Compose

### Running

The API is not containerised yet, so compose brings up the dependencies while the API runs from source.

```bash
git clone https://github.com/tthanhtung92/finmy.git
cd finmy

# create .env at the repo root from the template
cp .env.example .env

# bring up PostgreSQL, Redis and MinIO
docker compose -f docker/docker-compose.yml --env-file .env up -d
```

The three connection strings (`IdentityDb`, `BudgetingDb`, `LedgerDb`) and the MinIO credentials are empty in `appsettings.json`; supply them through the host's User Secrets:

```bash
dotnet user-secrets set "ConnectionStrings:IdentityDb" "<connection string>" --project src/Bootstrap/Finmy.Api
```

Migrations do not run at startup, so apply them per module:

```bash
dotnet ef database update -p src/Modules/Identity/Finmy.Identity.Infrastructure -s src/Bootstrap/Finmy.Api
dotnet ef database update -p src/Modules/Budgeting/Finmy.Budgeting.Infrastructure -s src/Bootstrap/Finmy.Api
dotnet ef database update -p src/Modules/Ledger/Finmy.Ledger.Infrastructure -s src/Bootstrap/Finmy.Api

dotnet run --project src/Bootstrap/Finmy.Api

# Scalar API docs: http://localhost:5079/scalar
# MinIO console:   http://localhost:9001
```

Wolverine's tables on the `wolverine` schema are created at startup and need no migration.

For pgAdmin and RedisInsight alongside, use `docker/docker-compose.local.yml`.

---

## Project layout

```text
finmy/
├── src/
│   ├── Bootstrap/Finmy.Api/        # the only host, composition root
│   ├── Modules/
│   │   ├── Identity/               # auth, JWT, refresh-token rotation
│   │   │   ├── Finmy.Identity.Domain/
│   │   │   ├── Finmy.Identity.Application/
│   │   │   ├── Finmy.Identity.Infrastructure/
│   │   │   └── Finmy.Identity.Api/
│   │   ├── Budgeting/              # envelopes and balances, caching, uploads, SignalR
│   │   └── Ledger/                 # transactions, Wolverine outbox, reversal, idempotency
│   └── Shared/
│       ├── Finmy.SharedKernel/     # Result<T>, Error, ErrorType
│       ├── Finmy.Modularity/       # IModule, ResultExtensions, ValidationFilter
│       └── Finmy.Contracts/        # integration events between modules
├── tests/
│   ├── Finmy.UnitTests/            # domain, services, cache and alert policies, validators
│   └── Finmy.IntegrationTests/     # real Postgres through Testcontainers
├── bench/                          # k6 script for the cache benchmark
├── docker/                         # compose files
└── docs/                           # ROADMAP, TECH-DEBT, naming conventions, ADRs
```

Space, Account and the rest arrive with the [roadmap](docs/ROADMAP.md).

---

## Testing

```bash
dotnet test Finmy.slnx          # 102 tests; the integration suite needs Docker
pwsh scripts/coverage.ps1       # coverage, failing below the recorded floor
```

Unit tests cover the Envelope domain (create, update, spend, fund), `EnvelopeService`, the cache and alert policies, the receipt validator and the Transaction domain. Architecture tests hold the module boundaries and the `Version++` invariant. Integration tests run against real containers: the concurrency race straight against Postgres, and the full anti-overspend loop over HTTP through `WebApplicationFactory` with Postgres, Redis and MinIO behind it, waiting on Wolverine's tracked session rather than on a sleep.

The test projects run on Microsoft Testing Platform rather than VSTest. Filters go after `--`: `--filter-class`, `--filter-method`, `--filter-query`. The older `--filter "FullyQualifiedName~X"` syntax is accepted, matches nothing, and reports "Zero tests ran" with exit code 5, which looks like a broken runner and is a broken filter.

Architecture tests run alongside the rest of the suite, so a broken module boundary shows up as a failing build rather than in review.

---

## Cache benchmark

Measured with k6 against `GET /envelopes`, comparing two states of the same endpoint: cache miss (the request goes all the way to Postgres) and cache hit (the response comes straight from the output cache).

Throughput and latency, 50 VUs for 30 seconds per state, `http_req_failed` at zero:

| Metric | Before cache (miss) | After cache (hit) | Difference |
| --- | --- | --- | --- |
| Throughput | 1238 req/s | 32722 req/s | about 26x |
| p95 latency | 58.5 ms | 3.0 ms | about 19x lower |
| p99 latency | 81.0 ms | 5.6 ms | about 14x lower |
| Mean latency | 40.2 ms | 1.4 ms | about 29x lower |

Payload after response compression, on a list with `pageSize=100`:

| Encoding | Size | Versus uncompressed |
| --- | --- | --- |
| None | 16545 B | 1x |
| Brotli (`br`) | 2517 B | 6.6x smaller |
| Gzip | 3313 B | 5.0x smaller |

Conditions: AMD Ryzen 7 4800H, Windows 11, host running .NET 10 in Release on localhost, k6 v2.1.0, 50 VUs, 30 seconds per state, roughly 60 seeded envelopes. Since k6 and the host share a machine with no real network in between, these numbers compare miss against hit on one configuration; they are not latencies a user would see over the internet.

---

## Architecture decisions

Significant decisions are recorded as ADRs:

- [ADR-0001: Modular Monolith instead of microservices](docs/adr/0001-modular-monolith.md)
- [ADR-0002: Wolverine as mediator, message bus and transactional outbox](docs/adr/0002-wolverine.md)
- [ADR-0003: Avoiding commercially licensed libraries; Mapster, NSubstitute, Shouldly](docs/adr/0003-avoid-commercial-libraries.md)
- [ADR-0004: Identity module boundary via Option A (dependency inversion through IIdentityService)](docs/adr/0004-identity-option-a.md)
- [ADR-0005: JWT short-name claims with IdentityClaimTypes as the source of truth](docs/adr/0005-jwt-short-name-claim.md)
- [ADR-0006: Moving the domain to shared envelope budgeting](docs/adr/0006-pivot-to-shared-budgeting.md)
- [ADR-0007: Naming conventions for folders, files and namespaces](docs/adr/0007-naming-conventions.md)
- [ADR-0008: Serving receipt images via presigned URLs with a CDN in front of the origin](docs/adr/0008-cdn-in-front-of-object-storage.md)
- [ADR-0009: An int `Version` column managed by the domain as the concurrency token, not `xmin`](docs/adr/0009-self-managed-version-concurrency-token.md)
- [ADR-0010: Budgeting owns the envelope balance; overspend protection is eventually consistent](docs/adr/0010-single-writer-envelope-balance.md)
- [ADR-0011: Recording a transaction is an async 202 Accepted with a status resource](docs/adr/0011-async-request-reply-202.md)

---

## Roadmap

The phase plan is in [docs/ROADMAP.md](docs/ROADMAP.md); known gaps are tracked in [docs/TECH-DEBT.md](docs/TECH-DEBT.md).

- [x] Foundations, solution layout, module skeleton
- [x] Identity: auth, JWT, refresh-token rotation
- [x] Budgeting: envelope CRUD and monthly report
- [x] HybridCache with tag invalidation, MinIO uploads, output caching, cache benchmark
- [x] SignalR realtime, Wolverine in-process, async 202 writes, transactional outbox
- [x] Overspend protection with a race-condition test, and the full event chain
- [x] Idempotency: `Idempotency-Key` on writes plus a consumer dedup table
- [x] Build and quality gates: analyzers, coverage, NetArchTest, HTTP-level integration tests
- [ ] Packaging and CI/CD: Dockerfile, GitHub Actions, image publishing
- [ ] Production hardening: health checks, authorization, rate limiting, API versioning, durable status store
- [ ] Observability: Serilog plus OpenTelemetry into a self-hosted Grafana stack
- [ ] Deployment: Helm on k3s, Terraform, TLS, encrypted secrets
- [ ] Space, membership and per-Space authorization

---

## License

Released under the [MIT License](./LICENSE).
