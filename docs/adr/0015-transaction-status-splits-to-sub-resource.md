# ADR-0015: The transaction status resource splits to a sub-resource and answers `303 See Other`

## Status

Accepted, 2026-08-05. Supersedes the route shape of [ADR-0011](0011-async-request-reply-202.md), not its state machine.

## Context

ADR-0011 designed the async `202 Accepted` contract for `POST /transactions`: `Location` pointed at `/transactions/{id}`, and `GET /transactions/{id}` was the status resource itself, answering a flat `200` at every stage. `Pending`, `Succeeded` and `Failed` all came back as `200` with a `status` field, so there was nowhere for a client to go once the request settled.

ADR-0011's own Consequences section named two things it left open: no `GET /transactions/{id}` returning the transaction itself, so nothing existed to redirect to with `303 See Other`, and no retention policy for the status resource. `docs/TECH-DEBT.md` tracked these as #8 and #3. This phase closed both. TECH-DEBT #2 moved the status store to Postgres first, which is what supplied the durable `ExpiresAtUtc` column this ADR's `Expires` header and pruning window both depend on. The route split described here landed on top of that.

## Options considered

**Keep `/transactions/{id}` as the status resource permanently, add a separate route such as `/transactions/{id}/full` for the transaction itself.** Rejected: the canonical `{id}` route returning a status envelope instead of the resource it names is the confusing shape, and it would leave TECH-DEBT #8 only half solved.

**Answer with `200` and a body carrying both the status and a link to the transaction, HATEOAS style, instead of a redirect.** Rejected: a heavier client contract for no real benefit here. `303 See Other` is the standard HTTP mechanism for exactly this case (`POST` accepted asynchronously, `GET` the result once it exists), per RFC 9110 §15.4.4.

**Keep `GET /transactions/{id}` answering `Pending` and `Succeeded` uniformly at `200` and skip the redirect.** Rejected: this is precisely the gap TECH-DEBT #8 named. A client polling the status had no way to fetch the settled transaction from that response.

## Decision

`POST /transactions` still returns `202 Accepted`, but `Location` now points at `/transactions/{id}/status` instead of `/transactions/{id}`.

`GET /transactions/{id}/status` is the status resource. `Pending` returns `200` with `Retry-After` and an `Expires` header (RFC 7231 HTTP date, sourced from `ExpiresAtUtc`). `Succeeded` returns `303 See Other` with `Location` pointing at `/transactions/{id}`. `Failed` keeps the existing `ProblemDetails` mapping unchanged, `409`, `400` or `500` depending on the error.

`GET /transactions/{id}` is new. It returns the transaction itself, `TransactionResponse` with `Id`, `SpaceId`, `EnvelopeId`, `Amount`, `Direction`, `State`, `OccurredOnUtc`, `Description`, `ConfirmedAtUtc` and `ReversedAtUtc`, or `404`. `Direction` and `State` cross the wire as numbers, matching the rest of the API: no `JsonStringEnumConverter` is registered anywhere in the host.

The `303` is a small, purpose-built `IResult` (`SeeOtherResult`) rather than `Results.Redirect`, since that helper only offers `301`, `302`, `307` and `308`, none of which is the status this case calls for.

The idempotent-replay branch of `POST /transactions`, the one taken when the same `Idempotency-Key` is reused, now also points its `202` `Location` at `.../status` and sets `Retry-After`, which it was missing before.

This is a breaking route change, and it rides along with the `/api/v1` prefix move already happening this phase, itself breaking, rather than shipping as a second breaking change later.

ADR-0011 is not edited in place; ADRs are immutable. Its status line now reads "Accepted, 2026-07-26 (route shape superseded in part by ADR-0015, 2026-08-05)" rather than a full supersession, since the rest of its decision stands unchanged: the `202 Accepted` contract, the `Pending`/`Succeeded`/`Failed` three-state model, the reasons the blocking-request and immediate-`201` options were rejected, `SendAsync` over `PublishAsync`, and the `TransactionRejectedException`/`Discard()` handling.

## Consequences

TECH-DEBT #8 is closed: the transaction is now fetchable. The `Expires` half of TECH-DEBT #3 is closed too: the retention window is now visible to a client, not just enforced server side by the pruning sweep.

Anyone consuming the old contract, `Location` pointing directly at the transaction and a flat `200` status at every stage, breaks and has to move to the two-route shape. Verified against real containers: `POST` returns `Location` ending in `/status`; `GET .../status` with redirects disabled returns `303` with `Location` pointing at the bare transaction URL; following it returns the transaction with `state: 2` (`Confirmed`).
