# ADR-0014: Migrations stay a pre-deploy job serialized by EF Core's own lock, defended by a readiness gate

## Status

Accepted, 2026-08-05.

## Context

Phase 2's `docker/docker-compose.yml` added a one-shot `migrate` service that applies EF migrations for all three modules before `api` starts, satisfying "clone and `docker compose up`, no manual steps." TECH-DEBT #16 flagged the gap that came with it: `migrate` is a single, unlocked writer. `dotnet ef database update` takes EF's own migration lock, so one instance racing another was expected to mostly resolve itself, but nothing in Phase 2 had actually confirmed that, and nothing stopped a replica from starting against a database mid rolling-deploy, with two nodes each triggering their own migration step.

Phase 3 needs a strategy that survives more than one `api` replica without bringing back the coupling `Database.Migrate()` at startup would cause.

## Options considered

**`Database.Migrate()` at application startup.** Every replica racing to migrate on boot is the exact problem this ADR exists to avoid, and it couples request-serving startup to however long a schema change takes.

**A hand-rolled distributed lock (`pg_advisory_lock`, or a Kubernetes coordination primitive) wrapped around the `migrate` entrypoint.** Worth testing before building: ran two `docker compose run --rm migrate` instances concurrently against a freshly created Postgres 17.10 database. Both exited 0. The first instance's log showed "Acquiring an exclusive lock for migration application" before every `Applying migration '...'` line, working through all pending migrations across the three schemas in sequence. The second instance's log showed three "Acquiring an exclusive lock for migration application" lines and no `Applying migration` lines at all: it waited behind the first instance's lock for each `DbContext`, found nothing left pending once it got the lock, and exited cleanly having applied nothing. This is EF Core's own migration lock (Npgsql's `HistoryRepository` implementation; see <https://aka.ms/efcore-docs-migrations-lock>), already in effect on EF Core 10.0.10 with `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3, no extra configuration needed. A hand-rolled lock on top would guard against a race that does not exist.

**Block replica startup on an `initContainer` alone, with no readiness check.** Defends the normal deploy path but not a replica that starts against a stale schema some other way, a manual `kubectl` action bypassing the deploy pipeline, say. A readiness probe still catches that case; an `initContainer` alone does not.

## Decision

Four things, together:

1. Migrations never run at application startup. `Program.cs` never calls `Database.Migrate()`; `FinmyApiFactory` migrates as fixture setup for the integration test suite, not as host behavior.
2. Exactly one migrator runs per deploy, as a job that must exit successfully before any replica starts. `docker/docker-compose.yml`'s `migrate` service already has this shape (`api` depends on `migrate` with `condition: service_completed_successfully`); the Helm `Job`/`initContainer` translation is Phase 5 work, out of scope here.
3. A replica that comes up against an un-migrated database fails readiness and takes no traffic. `DbContextHealthCheck<TContext>`'s `GetPendingMigrationsAsync()` branch, added this phase and wired into `GET /health/ready`, is what actually closes TECH-DEBT #16: the rolling-deploy window was the real failure mode, not the migration step itself.
4. Schema changes are expand/contract, so an old replica keeps working during the overlap between a new migration landing and every replica having rolled over.

No extra locking is added around the `migrate` entrypoint. EF Core's own lock, confirmed above, already serializes concurrent migrators.

## Consequences

TECH-DEBT #16 is closed: two migrators racing resolve safely today, and a replica that starts before migration completes cannot take traffic. The Helm `Job`/`initContainer` translation of the compose `migrate` service, and any tuning of the migration lock's timeout under real production load, stay open for Phase 5, when the k3s deployment is built.
