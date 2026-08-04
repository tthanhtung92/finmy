# ADR-0002: Wolverine as mediator, message bus and transactional outbox

## Status

Accepted, 2026-07-12.

Module and event names in this record predate [ADR-0006](0006-pivot-to-shared-budgeting.md). The `Ticketing` module became `Ledger`, and the anti-overselling problem became anti-overspend on budget envelopes. The library choice is unchanged.

## Context

Finmy needs three pieces of application infrastructure: a mediator so endpoints stay thin and handlers do the work, an in-process message bus to publish integration events between modules, and a transactional outbox so writing to the database and publishing an event are one atomic operation.

The last one is the crux. The project's signature problem is preventing oversale: placing an order has to write the Order and publish `TicketSoldEvent` with no window in which a crash leaves the two out of step.

A parallel constraint: several familiar .NET libraries moved to commercial licensing during 2025, so the choice has to weigh licensing alongside features. See [ADR-0003](0003-avoid-commercial-libraries.md).

## Options considered

**MediatR**, the most widely used mediator, but it moved to a commercial license in 2025, and it is only a mediator: a message bus would still be needed and the outbox would still have to be written by hand. Two problems in one.

**MassTransit**, a mature bus with an outbox, but it also moved to a commercial model in 2025. Rejected for the same licensing reason, and heavier than an in-process monolith needs.

**A hand-written mediator and outbox**, free of licensing concerns and fully under our control, but the effort goes into something that is not the point of the project. Writing an outbox correctly (atomic, retrying, idempotent) is a sub-project in itself, and it is easy to get wrong exactly where certainty matters most.

**Wolverine**, MIT licensed, combining mediator, message bus and a native transactional outbox in one library. All three needs from one dependency.

## Decision

Wolverine, as mediator, in-process message bus, and transactional outbox. Thin endpoints send commands and queries through Wolverine to handlers; integration events in `Finmy.Contracts` are published over the bus; the order flow uses Wolverine's native outbox so writing the Order and publishing its event are atomic.

On code generation: development uses Dynamic mode, which compiles handlers with Roslyn at startup. The intent, not yet acted on, is to switch to Static codegen for production images to avoid recompiling on every cold start and to drop Roslyn's memory overhead.

## Consequences

One dependency covers all three needs, which means less integration surface than stitching three libraries together, and the outbox does not have to be hand-written at the most sensitive point in the system.

The native outbox serves the concurrency work directly: an atomic write-and-publish is the foundation that optimistic concurrency and idempotent consumers build on.

The trade-off is a commitment to a codegen-heavy library. Misunderstanding the codegen mode can slow cold starts or inflate the image, so codegen mode is a deliberate decision when moving to production rather than something left at its default.
