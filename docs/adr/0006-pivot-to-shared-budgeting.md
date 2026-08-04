# ADR-0006: Move the domain to shared personal finance using envelope budgeting

## Status

Accepted, 2026-07-16

## Context

Finmy is built by one person, with the goal that each core backend concept (auth, caching, CDN, realtime, messaging, concurrency) gets a thin vertical slice that runs for real and can be explained. The problem running through all of it is contention: several people writing to one scarce resource at the same time. In the original domain that meant preventing oversold tickets.

The original domain was event ticketing, and the trouble with it was that the author would never actually run it. There are no events to sell tickets for, so the project could only ever be a one-off exercise. It lacked the pressure that comes from a system having real users, which is what forces a system to be correct and to keep working. Even with a UI on top, nobody would open it.

That set three constraints for choosing a new domain:

1. The domain has to be something the author **actually runs**, self-hosted for a real sharing group, so there is a reason to keep it correct.
2. It must not lose the existing technical surface: all eight concepts stay covered, and the contention slice, the core of the project, survives.
3. It must not be a worse clone of a mature incumbent. If good free tooling already exists, the author will use that instead, and the "actually runs it" driver disappears.

## Options considered

**Keep event ticketing.** The Ticketing and anti-oversell slice already exists, but the author runs no events, so it stays an exercise nobody uses daily, UI or not.

**Booking or reservation of a limited resource.** Preserves the contention problem exactly, since double-booking maps one-to-one onto overselling. But daily real use is weak unless the author actually manages a specific shared resource.

**Developer tools** (webhook inspector, request runner, synthetic monitor, secret sharing). Useful to a backend developer, but every idea has a mature free incumbent (webhook.site, Postman, Uptime Kuma, PrivateBin). A self-hosted version would simply be worse, which breaks constraint 3, and real usage frequency would be low.

**A personal notification or watcher hub.** It has a genuine async core (scheduled polling, dedup, outbox), but it runs into Uptime Kuma and Google Alerts, and many sources need messy HTML scraping that tends to hit bot protection.

**Shared personal finance using envelope budgeting**: a group spends from a common pot, with money divided into budget envelopes. There is real daily demand, and it escapes constraint 3 for three reasons. Financial data is worth self-hosting for privacy. Local needs (VND, Vietnamese banks and e-wallets) are poorly served by YNAB or Actual. And a self-written version can be tailored to one specific group. Firefly III, Actual and Money Lover exist, but the pull toward "just use the existing one" is much weaker here.

Within the finance direction there were two sub-choices.

**Solo tracking versus group sharing.** Solo tracking is essentially plain CRUD, and a concurrency problem would have to be bolted on artificially. A shared group budget makes concurrency and realtime meaningful, since several members spend from the same envelope.

**Single-entry versus double-entry ledger.** The concurrency slice sits on the Envelope balance and does not depend on the ledger model. Single-entry is sufficient and simpler; double-entry offers more accounting rigour at more complexity without adding concurrency.

## Decision

Finmy's domain moves to **shared personal finance using envelope budgeting**, with a **single-entry ledger**, keeping all eight concepts in the MVP scope.

The central aggregate is **Space**, the root for sharing, owning Account, Category, Envelope and Transaction. **Member** binds a user to a Space with an Owner, Member or Viewer role, and the authorization boundary is that a user only touches data in their own Space. A Space is a flat sharing group, not tied to any particular kind of group: a house share, a trip fund, or a small team are all just a Space.

The contention problem replacing anti-oversell is **preventing an Envelope from being overspent when several members spend concurrently**, using optimistic concurrency on the Envelope balance. That maps one-to-one onto the old concurrency slice.

The module map changes theme while keeping the architecture. Identity is unchanged. Events becomes **Budgeting** (accounts, categories, envelopes), keeping the CRUD, HybridCache and repository-port pattern. Ticketing becomes **Ledger** (transactions), keeping the Wolverine outbox, concurrency, idempotency and SignalR. SharedKernel, Contracts, the infrastructure and Central Package Management are unchanged.

Concept slices map across as follows. Caching becomes report and aggregate caching with invalidation. CDN and object storage becomes MinIO holding receipt images. Realtime becomes SignalR pushing new balances to members of the same Space. Messaging becomes async transaction posting plus recurring transactions through Wolverine scheduling and the transactional outbox. Concurrency becomes optimistic concurrency on Envelope. Idempotency becomes deduplication when importing CSV statements.

## Consequences

The domain now has a real driver, since the author self-hosts their own shared budget. The pressure to keep it correct comes from having to use it.

The technical surface is unchanged: all eight concepts and the concurrency slice remain. Most of the Identity, infrastructure and architecture work carries over, and the cost of the pivot is confined to re-theming two modules.

The concurrency problem becomes a real situation, several people spending one shared budget, rather than a scenario invented to have one.

The decisions in ADR-0001, 0002, 0004 and 0005 all still stand. Only two module names change theme, which is a rename rather than a reversal, so no ADR is superseded.

One trade-off needs active management: most of the volume in a finance app is CRUD and reporting. Without deliberately keeping the envelope-concurrency and import-idempotency slices at the centre, the project drifts into an ordinary CRUD app and loses its depth.

Single-entry gives up double-entry's balancing property. If strict transfer entries or reconciliation are needed later, the ledger has to be upgraded, which would be its own decision.

Automatic import from Vietnamese banks is not viable without widespread open banking, so the import slice is fed by CSV or statement upload, or manual entry.

Money has to be handled properly: `decimal`, stored in the smallest unit, with rounding treated carefully. That is new work relative to the ticketing domain.

Left open, each to get its own ADR when settled: a double-entry ledger, a frontend, and the full extent of running it for real (email, Vietnamese e-wallets, multi-currency, deployment).
