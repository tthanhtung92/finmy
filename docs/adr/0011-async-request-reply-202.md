# ADR-0011: Recording a transaction is an async `202 Accepted` with a status resource

## Status

Accepted, 2026-07-26. Written down on 2026-08-04, after the fact.

## Context

[ADR-0010](0010-single-writer-envelope-balance.md) put the envelope balance in Budgeting and the transaction in Ledger, which means `POST /transactions` cannot know at request time whether the spend will be accepted. The endpoint has to answer before the answer exists.

## Options considered

**A. Async request-reply.** Return `202 Accepted` with a `Location` header pointing at a status resource the client polls.

**B. Block the request** until the consumer has applied the deduction, then return the final result.

**C. Return `201 Created` immediately** and let the client find out about a reversal some other way.

## Decision

Option A. Option B reintroduces synchronous coupling across the module boundary and turns a queue into a blocking call, which is what ADR-0010 spent the boundary to avoid. Option C reports success for something that may be reversed a moment later, so the client acts on an answer the system has not reached yet.

Four details came with it.

The endpoint generates the `TransactionId` with `Guid.CreateVersion7()` and passes it into both the command and the factory, rather than minting a separate tracking `operationId`. One identifier is easier to follow, and idempotency needs a key that is known before the write happens.

Status is written as `Pending` before the message is sent. Local queues run in parallel, so doing it the other way round lets the handler finish and write `Succeeded` before the request thread writes `Pending`, leaving the request stuck as pending forever.

The endpoint uses `SendAsync`, not `PublishAsync`. `SendAsync` requires at least one subscriber and throws when there is none; `PublishAsync` silently drops. For a command, "nobody is listening" is a serious fault that should surface immediately.

A rejected transaction throws `TransactionRejectedException`, and the handler chain applies `Discard()`. `MoveToErrorQueue()` was rejected because the failure information already lives in the status store. Logging and swallowing was rejected because it makes every error-rate metric read zero while real user transactions are being refused.

## Consequences

The client contract is now three states rather than two, and the intermediate one is visible: `Pending` until Budgeting has applied the deduction, then `Succeeded` or `Failed`. Hiding `Pending` behind an early success would defeat the point.

Open items this leaves behind are tracked in [TECH-DEBT.md](../TECH-DEBT.md): the status store is still in memory and outside the transaction, there is no `GET /transactions/{id}` to redirect to with `303 See Other`, and there is no retention policy for status records.
