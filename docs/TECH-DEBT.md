# Technical debt register

Debt taken on deliberately while building Finmy, with the reason each item was deferred. Extracted from per-day review notes on 2026-08-04 and re-checked against the current code. Anything already paid off has been dropped from this list.

The Phase column points at the roadmap in [ROADMAP.md](ROADMAP.md).

## Blocks production

| # | Debt | Phase |
|---|---|---|
| 1 | **Budgeting and Ledger endpoints have no `RequireAuthorization()`.** Only `IdentityDemoEndpoints.cs` has it. Anyone can `POST /transactions`, and anyone who knows a `transactionId` can read its status. The HTTP tests call those endpoints without a token, so closing this gap also means teaching `FinmyApiFactory` to mint one and send it on every request. | 3 |
| 2 | **`InMemoryTransactionStatusStore` keeps request state in RAM.** Lost on restart, wrong across instances. The store also sits outside the transaction, so it can report a state the database never reached: if the commit fails after the handler wrote `Succeeded`, the client still reads `Succeeded` for a transaction that does not exist. The `ITransactionRequestStatusStore` port stays; only the adapter moves to Postgres. | 3 |
| 3 | **No retention policy for the status resource.** Once the store lives in the database, every request leaves a row behind forever. MS guidance suggests an `Expires` header so clients know the retention window. | 3 |
| 4 | **`AutoBuildMessageStorageOnStartup` left at its default**, so Wolverine creates its tables at startup. Production should use `AutoCreate.None` and run `resources setup` as an explicit deploy step. | 5 |

## Correctness and operations

| # | Debt | Phase |
|---|---|---|
| 7 | **`Transaction.EnvelopeId` is never checked for existence.** There is no FK to `budgeting.envelopes`, deliberately, since a cross-schema FK would hard-wire two modules together, and nothing replaces it. A transaction pointing at a nonexistent envelope currently lands in the dead letter queue instead of being reversed automatically. | 6 |
| 8 | **No `GET /transactions/{id}` returning the transaction itself**, so the status endpoint cannot answer `303 See Other` on completion. The data is in the database now, so this is a matter of choosing to do it rather than being unable to. | 3 |
| 9 | **`UnreachableException` in the `default` arm of the `TransactionDirection` switches** (`TransactionPostedHandler.cs:42`, `RecordTransactionHandler.cs:65`) relies on a guarantee that does not hold. `Enum.IsDefined` only guards the creation path. Messages travel over the wire as numbers and can be replayed from the outbox after a new enum member is added, at which point the exception name describes something other than what happened. | |
| 10 | **Envelope alerts are level-triggered.** Once an envelope drops below the 20% threshold, every later transaction fires an alert instead of firing once on the crossing. Edge-triggering needs previous state (a column on `Envelope` or an alert history table) plus an answer to the harder question of how it resets at the start of a new period. | |

## Waiting on the Space aggregate

| # | Debt | Phase |
|---|---|---|
| 11 | **SignalR groups are keyed per envelope and need to move to per space.** The seam is already in place: group names are produced in exactly one spot, `EnvelopeGroups.ForEnvelope(Guid)`. With Space, three things change. Add `SpaceId` to `Envelope` (migration plus a default space for existing envelopes), switch the group key from `envelope-{id}` to `space-{spaceId}`, and have the client `invoke("WatchSpace", spaceId)` instead of watching each envelope. Hub, port, adapter and DI stay as they are. | 6 |
| 12 | **Output caching works today only because the report and list endpoints are anonymous.** Once they are behind authorization, output caching skips requests carrying an `Authorization` header by default, which is the safety net against serving one user's cache to another. The choice then is to drop output caching on authorized endpoints, or write a policy that varies by identity so each user gets their own slot. | 3 |
| 13 | **Anything summing spend has to filter on `TransactionState`.** Since `Reversed` exists, every rollup needs the filter or reversed transactions keep counting. | 6 |

## Configuration ownership

| # | Debt | Phase |
|---|---|---|
| 14 | **Untested hypothesis:** if `AutoApplyTransactions()` picks up a handler in a module registered with a plain `AddDbContext` rather than `AddDbContextWithWolverineIntegration`, the middleware may call `SaveChangesAsync` without enrolling that `DbContext` in the outbox. If so, dual-write returns while the build stays green. Still open, though narrower now: the HTTP tests fail when `AutoApplyTransactions()` is removed, so the policy itself is covered. What is not covered is a module wired the wrong way, and there is no third module with a handler to write that case against. | 6 |

## Notes worth keeping, not debt

- **`Remaining` is a computed property with no database column.** It cannot appear directly in a LINQ `WHERE` or `ORDER BY`; write `e.Allocated - e.Spent` instead.
- **Wolverine codegen constructs the `DbContext` inline in the handler body**, with no `CreateScope()`. The generated constructor takes `DbContextOptions<T>` and the first line of `HandleAsync` is `new BudgetingDbContext(...)`. Every run, including an inline retry, gets an empty change tracker, which is why `RetryWithCooldown` is correct here and `Requeue()` is unnecessary.
- **`EnvelopeRepository` has to be `public`** because codegen emits `new EnvelopeRepository(...)` inside the `Internal.Generated.WolverineHandlers` assembly, where `internal` will not compile. It is not public for the sake of tests.
- **`HasData` seed values must be constant.** EF diffs seed data between migrations, so `Guid.NewGuid()` or `DateTime.Now` inside `HasData` makes every `migrations add` see changed data and emit a pointless update.
- **Cache invalidation is best-effort after commit.** If `RemoveByTagAsync` throws because Redis is down, the exception fires after the database commit: the write is persisted but the caller sees an error, and the cache holds the stale copy until the TTL expires. Two ways out are to accept staleness until TTL, or to move cache eviction to an out-of-band path with retries.
