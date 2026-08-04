# ADR-0010: Budgeting owns the envelope balance; overspend protection is eventually consistent with a compensating reversal

## Status

Accepted, 2026-07-25. Written down on 2026-08-04, after the fact. The decision was made when the Ledger module was designed and has shaped everything built since, but it only ever lived in day notes. [ADR-0009](0009-concurrency-token-version-tu-quan.md) already cites it as settled without pointing anywhere.

## Context

The problem this project is built around is stopping a shared budget envelope from being overspent. Two modules touch that problem, and the boundary rule keeps them apart: `Transaction` lives in Ledger, `Envelope` lives in Budgeting, and neither module may reference the other's Domain or Infrastructure.

That rules out the obvious answer. A single database transaction spanning "record the transaction" and "deduct from the envelope" would require one module to reach into the other's tables, which is the coupling the architecture exists to prevent.

So the question is which module owns the balance, and what "overspend is rejected" is allowed to mean once the write and the check cannot be atomic.

## Options considered

**A. Budgeting owns the balance.** Ledger records the transaction and publishes `TransactionPostedEvent`; a consumer in Budgeting deducts under optimistic concurrency and rejects when funds are short.

**B. Ledger keeps a replica of the balance** and deducts synchronously at write time, with Budgeting's copy treated as a projection.

**C. Move the balance into Ledger** so transaction and balance live in one aggregate and one transaction.

## Decision

Option A, on the single-writer rule: a piece of state has exactly one owner allowed to mutate it, and the balance is inseparable from `Envelope`, which lives in Budgeting.

Option B was rejected because it creates two writers for one number. That is the dual-write problem again: the replica drifts and nothing reports it. Option C was rejected because it drags the entire budgeting concept into the ledger and inverts the domain model to buy atomicity in one code path.

Four consequences follow directly from that choice.

**Overspend protection is eventually consistent.** Ledger writes first, Budgeting checks afterwards and refuses, Ledger reverses. There is a window in which the system is internally inconsistent, and a transaction can exist as recorded but not yet reflected in a balance.

**A rejected transaction is reversed, not deleted**, using `TransactionState` plus a `ReversedAtUtc` timestamp. A ledger that can delete rows loses the ability to reconstruct its own history, and nothing in the system reports that loss. This is not a proper contra entry, since [ADR-0006](0006-pivot-sang-tai-chinh-chia-se.md) settled on single-entry, but it keeps the trail. `Reverse` refuses a second reversal and the handler swallows exactly that error, so a redelivered message is a no-op. The outbox only guarantees at-least-once, so duplicates will happen.

**The two kinds of failure take different routes**, and this is the part that is easiest to get wrong. Insufficient funds is a valid business conclusion, so it raises `EnvelopeOverspentEvent` and the other side reverses. A missing envelope is corrupt data, so it throws and the message goes to the dead letter queue. Automatically compensating for the symptom of a bug would hide the bug.

**Retry policy placement follows a rule.** `DbUpdateConcurrencyException` retries are configured in `Program.cs`, not in the handler's `Configure(HandlerChain)`, because that exception type belongs to `Microsoft.EntityFrameworkCore` and putting the policy on the handler would force `Finmy.Budgeting.Application` to reference EF Core, the exact dependency the `IEnvelopeRepository` port exists to avoid. The general rule: policy about a message's business meaning goes on the handler, policy about infrastructure goes on the host.

## Consequences

The cost is that "you cannot overspend" is enforced one beat after the request rather than during it. The API therefore has to expose the intermediate state honestly. A transaction is `Pending` until Budgeting has applied the deduction, and reporting success at write time would tell the client something the system has not yet decided.

The benefit is that the module boundary survives contact with the hardest requirement in the system, and the mechanism generalises. Any future module that needs to react to a transaction subscribes to the same contract instead of reaching into Ledger.

Reversing this decision later is expensive. It determines the shape of the outbox, the consumers, the concurrency token in [ADR-0009](0009-concurrency-token-version-tu-quan.md), and the async `202 Accepted` API in [ADR-0011](0011-async-request-reply-202.md). It is not a local choice.
