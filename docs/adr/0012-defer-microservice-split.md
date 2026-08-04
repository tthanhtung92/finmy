# ADR-0012: Stay a modular monolith through the production phases; extract Identity first if a split becomes necessary

## Status

Accepted, 2026-08-04.

This does not supersede [ADR-0001](0001-modular-monolith.md). That record chose the modular monolith on grounds of build effort and a one-command startup. This one revisits the same boundary now that the project is being taken to a production deployment, and settles the order and the preconditions of a split, should one happen.

## Context

Phase 1 closed on 2026-08-04 and [ROADMAP.md](../ROADMAP.md) puts packaging and CI/CD next. Splitting the three modules into separate services was raised before that work starts, since the packaging decisions differ sharply between one deployable and three.

The operational facts today: one operator, one VPS, one group of real users, and no module whose load profile differs from the rest. Microservices buy independent deploys, independent scaling and team autonomy, and none of the three answers a problem the system currently has.

Much of the mechanical work is already done, which is the return on ADR-0001. Each module owns a `DbContext` on its own schema (`identity`, `budgeting`, `ledger`) behind its own connection string (`IdentityDb`, `BudgetingDb`, `LedgerDb`), so pointing a module at a separate database instance is a configuration change. Cross-module traffic already runs as integration events through `Finmy.Contracts`, and `Finmy.ArchitectureTests` fails the build on anything else. The transactional outbox and idempotent consumers, both of which a network hop would make mandatory, are in place.

What is not done is everything that currently works because the modules share a process:

- Wolverine runs on `UseDurableLocalQueues()` with a single message store, persisted against the `LedgerDb` connection string on schema `wolverine`. All three connection strings resolve to the same Postgres instance, so Budgeting's Wolverine-integrated `DbContext` enlists in a store that nominally belongs to Ledger. Separate services need a real transport and a message store each.
- HybridCache keeps its L1 in process. Tag invalidation on a write reaches every reader today; across services it would have to travel over the bus.
- SignalR has no backplane configured, so a second instance would not see the first instance's groups.
- `InMemoryTransactionStatusStore` holds the async `202` status resource of [ADR-0011](0011-async-request-reply-202.md) in process memory. It is already recorded in [TECH-DEBT.md](../TECH-DEBT.md); a split turns it from debt into a blocker.
- JWTs are validated once, in the single host. Each service would validate independently, against a shared signing key or a published key set.
- `FinmyApiFactory` boots one host and drives the whole anti-overspend loop over HTTP against it. Three hosts break that, and the coverage from Phase 1 goes with it.

Separately, the work in Phases 2 through 5 (container image, CI, health checks, observability, k3s) is not groundwork a split would let the project skip. It is the precondition for one. Running services across a network without distributed tracing and without a pipeline per service leaves the operator unable to see where a request failed.

## Options considered

**Split all three modules into services now.** Buys independent deploys and scaling the project has no use for. It multiplies Dockerfiles, pipelines, charts and dashboards by three, promotes distributed tracing from a Phase 4 goal to a hard prerequisite, and forces the six in-process assumptions above to be solved before anything ships. It also converts network partitions and partial failure into ordinary operating conditions for a system one person runs.

**Split Ledger and Budgeting, keep Identity in the host.** The most expensive pair to separate. Those two carry the anti-overspend loop, the deepest logic in the codebase and the one path where a compensating reversal already spans a bus hop. Distributing it costs the most and returns the least, while Identity, the module that would be cheap to extract, stays put.

**Stay a modular monolith through Phase 5, then extract Identity if a split is still wanted.** Defers a decision with no forcing function behind it, and keeps the boundary work continuing under the architecture tests in the meantime. The risk is that "later" never comes and the option is theoretical, which the module boundaries and the extraction order below are meant to keep honest.

## Decision

Stay a single deployable through Phase 5. The roadmap does not change: Phases 2 through 5 are the preconditions of any split, so nothing on them is wasted if one happens.

If a split becomes necessary, Identity goes first. It is a genuine leaf: it references `Finmy.Contracts` nowhere, publishes and consumes no integration event, takes no dependency on Wolverine, and reaches other modules only through the `IIdentityService` inversion of [ADR-0004](0004-identity-option-a.md). Extracting it needs a host, its own database, and a way for the other services to validate its tokens. It needs no transport migration. Budgeting and Ledger, which hold the anti-overspend loop, stay in one process.

A split is justified by a specific operational trigger: a second team owning a module, or one module whose scaling needs diverge from the others. Neither exists now, and neither is anticipated in the roadmap.

## Consequences

Phase 2 packages one image, one compose service and one pipeline, which is what the roadmap already assumed.

The extraction cost stays roughly where it is rather than falling, because the six in-process couplings above are not on the critical path for anything else. Three of them (the status store, the SignalR backplane, the JWT validation gap) are already scheduled in Phase 3, so that part of the cost gets paid regardless.

Deferring keeps the option only as long as the boundaries hold. `Finmy.ArchitectureTests` is what makes that a mechanical guarantee rather than an intention, so the shared Wolverine message store is worth watching: it is the one place where two modules touch the same storage, and it sits below the level the boundary tests inspect.

The accepted trade-off is unchanged from ADR-0001: no independent deploys, no per-module scaling, and a single blast radius for a bad release. What has changed since ADR-0001 is that the deployment those terms apply to will be reachable by its users, so a bad release costs them a working system rather than costing one developer a restart. Phase 3's health checks and Phase 4's observability are what keep that tolerable.
