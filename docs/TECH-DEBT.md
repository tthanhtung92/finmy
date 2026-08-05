# Technical debt register

Debt taken on deliberately while building Finmy, with the reason each item was deferred. Extracted from per-day review notes on 2026-08-04 and re-checked against the current code. Anything already paid off has been dropped from this list.

The Phase column points at the roadmap in [ROADMAP.md](ROADMAP.md).

## Blocks production

| # | Debt | Phase |
|---|---|---|
| 4 | **`AutoBuildMessageStorageOnStartup` left at its default**, so Wolverine creates its tables at startup. Production should use `AutoCreate.None` and run `resources setup` as an explicit deploy step. | 5 |

## Correctness and operations

| # | Debt | Phase |
|---|---|---|
| 7 | **`Transaction.EnvelopeId` is never checked for existence.** There is no FK to `budgeting.envelopes`, deliberately, since a cross-schema FK would hard-wire two modules together, and nothing replaces it. A transaction pointing at a nonexistent envelope currently lands in the dead letter queue instead of being reversed automatically. | 6 |
| 9 | **`UnreachableException` in the `default` arm of the `TransactionDirection` switches** (`TransactionPostedHandler.cs:42`, `RecordTransactionHandler.cs:65`) relies on a guarantee that does not hold. `Enum.IsDefined` only guards the creation path. Messages travel over the wire as numbers and can be replayed from the outbox after a new enum member is added, at which point the exception name describes something other than what happened. | |
| 10 | **Envelope alerts are level-triggered.** Once an envelope drops below the 20% threshold, every later transaction fires an alert instead of firing once on the crossing. Edge-triggering needs previous state (a column on `Envelope` or an alert history table) plus an answer to the harder question of how it resets at the start of a new period. | |
| 19 | **`finmy.envelope.concurrency_conflicts` only counts the HTTP CRUD path.** `EnvelopeRepository.SaveChangesAsync` is the only place that catches `DbUpdateConcurrencyException` and increments the counter, but on the message path `AutoApplyTransactions()` saves after `TransactionPostedHandler` returns, so that conflict is caught by Wolverine's own host-level retry policy in `Program.cs` and never reaches this counter. The "Concurrency conflicts" dashboard panel and alert rule are annotated with the gap; closing it needs either the save moved inside the handler's own try/catch, which fights how `AutoApplyTransactions()` is meant to be used, or a confirmed, stable Wolverine metric name for retried exceptions by type. See [ADR-0017](adr/0017-observability-shape.md). | |
| 20 | **The correlation id set by `CorrelationIdMiddleware` does not reach Wolverine handler logs.** It lives on the HTTP request's own `Activity` tag; handler spans belong to the same trace and so share the correct `trace_id`, but `ActivityEnricher` falls back to the trace id, not the caller-supplied correlation id, once execution moves into a message handler. Fixing it needs the correlation id carried as a Wolverine message header or in `Activity` baggage rather than a plain tag. See [ADR-0017](adr/0017-observability-shape.md). | |

## Waiting on the Space aggregate

| # | Debt | Phase |
|---|---|---|
| 11 | **SignalR groups are keyed per envelope and need to move to per space.** The seam is already in place: group names are produced in exactly one spot, `EnvelopeGroups.ForEnvelope(Guid)`. With Space, three things change. Add `SpaceId` to `Envelope` (migration plus a default space for existing envelopes), switch the group key from `envelope-{id}` to `space-{spaceId}`, and have the client `invoke("WatchSpace", spaceId)` instead of watching each envelope. Hub, port, adapter and DI stay as they are. | 6 |
| 13 | **Anything summing spend has to filter on `TransactionState`.** Since `Reversed` exists, every rollup needs the filter or reversed transactions keep counting. | 6 |

## Configuration ownership

| # | Debt | Phase |
|---|---|---|
| 14 | **Untested hypothesis:** if `AutoApplyTransactions()` picks up a handler in a module registered with a plain `AddDbContext` rather than `AddDbContextWithWolverineIntegration`, the middleware may call `SaveChangesAsync` without enrolling that `DbContext` in the outbox. If so, dual-write returns while the build stays green. Still open, though narrower now: the HTTP tests fail when `AutoApplyTransactions()` is removed, so the policy itself is covered. What is not covered is a module wired the wrong way, and there is no third module with a handler to write that case against. | 6 |

## Packaging and local tooling

| # | Debt | Phase |
|---|---|---|
| 15 | **`docker/docker-compose.yml` ships a development-only `Jwt__SigningKey` default in a tracked file**, so `docker compose up` works with no `.env` at all. Fine for a laptop; a real deployment needs a real secret supplied through the environment, which is Phase 5's Sealed Secrets or SOPS work, not this file. | 5 |
| 17 | **`docker/docker-compose.local.yml` is a standalone duplicate of `docker-compose.yml`, not an override layer**, so it drifted out of sync the moment `api` and `migrate` were added to the base file. It also maps pgadmin to host port 8080, which `api` now uses, so the two compose files cannot run together as they stand. | |
| 18 | **The production image ships Roslyn.** `TypeLoadMode.Auto` ([ADR-0013](adr/0013-wolverine-auto-codegen-in-production.md)) generates handler wrappers at runtime when no pre-built types are on disk, so `WolverineFx.RuntimeCompilation` has to be in the image. Baking `codegen write` output in at build time would drop it, at the cost of a two-pass build; worth it only if image size or cold start become measured problems. | 5 |

## Notes worth keeping, not debt

- **`Remaining` is a computed property with no database column.** It cannot appear directly in a LINQ `WHERE` or `ORDER BY`; write `e.Allocated - e.Spent` instead.
- **Wolverine codegen constructs the `DbContext` inline in the handler body**, with no `CreateScope()`. The generated constructor takes `DbContextOptions<T>` and the first line of `HandleAsync` is `new BudgetingDbContext(...)`. Every run, including an inline retry, gets an empty change tracker, which is why `RetryWithCooldown` is correct here and `Requeue()` is unnecessary.
- **`EnvelopeRepository` has to be `public`** because codegen emits `new EnvelopeRepository(...)` inside the `Internal.Generated.WolverineHandlers` assembly, where `internal` will not compile. It is not public for the sake of tests.
- **`HasData` seed values must be constant.** EF diffs seed data between migrations, so `Guid.NewGuid()` or `DateTime.Now` inside `HasData` makes every `migrations add` see changed data and emit a pointless update.
- **Cache invalidation is best-effort after commit.** If `RemoveByTagAsync` throws because Redis is down, the exception fires after the database commit: the write is persisted but the caller sees an error, and the cache holds the stale copy until the TTL expires. Two ways out are to accept staleness until TTL, or to move cache eviction to an out-of-band path with retries.
