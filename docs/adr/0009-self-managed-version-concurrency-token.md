# ADR-0009: Use an int `Version` column incremented by the domain as the concurrency token, not `xmin`

## Status

Accepted, 2026-07-29

## Context

Giving `Envelope` a balance introduced `Spent`, a `Remaining` computed from `Allocated - Spent`, and the `Spend`, `Release` and `Fund` methods. The invariant to hold is `0 <= Spent <= Allocated`: nobody spends past what was allocated.

That invariant does not survive concurrent access on its own. Two transactions spending from the same nearly-empty envelope both read the same `Spent`, both add their own amount, and both write back. The later write overwrites the earlier one and one of the two amounts disappears from the books. This is the classic lost update, and it happens even when each write sits in its own transaction, because neither transaction knows the other exists.

Constraints at the time of the decision:

- The balance is written only by Budgeting (the single-writer rule, [ADR-0010](0010-single-writer-envelope-balance.md)). Ledger publishes an event and waits; it has no path that writes the envelope table.
- The two modules cannot share a transaction, because `Envelope` belongs to Budgeting and `Transaction` to Ledger, and the boundary rule forbids either from seeing the other's database. The invariant has to be enforced where the write happens, in Budgeting.
- The stack is EF Core 10 on PostgreSQL 17 through Npgsql, with Wolverine in between. Whatever mechanism is chosen has to work when the write arrives through a message handler, not only through an HTTP request.
- `docs/ROADMAP.md` says "optimistic concurrency (rowversion)". "Rowversion" there means a token that prevents overwrites generally, not one specific API.

## Options considered

**Postgres's system column `xmin`, mapped with `IsRowVersion()`.** Every Postgres row carries `xmin`, holding the id of the transaction that produced that version, and it changes on every update; Npgsql maps it to a `uint` property ([Npgsql, Concurrency Tokens](https://www.npgsql.org/efcore/modeling/concurrency.html)). The advantage is real and significant: the database increments it, so nobody can forget. Three problems against it. EF generates an `AddColumn<uint>(name: "xmin", ...)` that has to be deleted by hand, or `database update` fails with `column name "xmin" conflicts with a system column name` ([efcore.pg#3270](https://github.com/npgsql/efcore.pg/issues/3270), [#145](https://github.com/npgsql/efcore.pg/issues/145), neither answered by a maintainer). The PostgreSQL documentation itself advises against relying on transaction id uniqueness over the long term ([System Columns](https://www.postgresql.org/docs/17/ddl-system-columns.html)), and the value does not survive a dump and restore or a logical replica. And no major ORM goes this way: Hibernate has `@Version`, Rails has `lock_version`, [Marten has `mt_version`](https://martendb.io/documents/concurrency), all explicit columns. Marten is the strongest signal, since it comes from JasperFx alongside Wolverine and runs only on Postgres, meaning its authors know `xmin` exists and still built their own column.

**A `Guid` token, regenerated on every write.** What Marten does. It helps in distributed systems because no global counter has to be coordinated. In a single-node monolith it buys nothing and is harder to read when inspecting rows with `psql`.

**No token at all, using a conditional `UPDATE`.** EF Core 7 and later have `ExecuteUpdateAsync`, which can express "add `amount` to `Spent` on the row with this `Id`, where `Allocated - Spent >= amount`" and then read the affected row count: 1 means there were funds, 0 means there were not. This is the fastest option available: one round trip, no prior read, never a conflict, never a retry. The price is that the invariant leaves the domain model and becomes a `WHERE` clause, so reading `Envelope.Spend` in C# no longer shows the overspend rule anywhere.

**An `int Version` column incremented by the domain, declared with `IsConcurrencyToken()`.** The rule stays in the domain, the mechanism behaves identically whether the write comes from HTTP or a message handler, and the generated migration is clean with nothing to hand-edit. In exchange, incrementing it is the developer's responsibility.

## Decision

An `int Version` column on `Envelope`, incremented by the domain, declared with `IsConcurrencyToken()` in `BudgetingDbContext.OnModelCreating`.

Two points shape the implementation.

**Increment in every state-changing method, including `Update`, which only edits the name and description.** EF Core puts the concurrency token in the `WHERE` clause of every `UPDATE` regardless of which columns changed, so an `Update` that does not increment lets two people renaming the same envelope overwrite each other silently. That is still a lost update; it has only moved from the money column to the name column. Hibernate and Rails also increment on every save rather than selectively per column.

**The retry policy for `DbUpdateConcurrencyException` lives in the composition root, not on the handler chain.** That exception type belongs to `Microsoft.EntityFrameworkCore`, and putting the policy on the handler would force `Finmy.Budgeting.Application` to reference EF Core, which is exactly what the `IEnvelopeRepository` port exists to prevent. The reusable rule: policy about a message's business meaning goes on the handler, policy about infrastructure goes on the host.

## Consequences

Forgetting `Version++` in a new mutating method removes the protection while the build stays green and nothing reports it. That is the direct price of not letting the database do the incrementing. The compensating rule: every new mutating method needs a unit test asserting the version increments.

The conflict does not come from Postgres. An `UPDATE` matching zero rows is a perfectly valid database result; EF Core counts the affected rows and raises `DbUpdateConcurrencyException` itself ([EF Core, Handling Concurrency Conflicts](https://learn.microsoft.com/en-us/ef/core/saving/concurrency)). Anyone looking for conflict traces in the Postgres log will find nothing.

The mechanism covers `UPDATE` and `DELETE` but not `INSERT`, because a new entity has no prior value to compare against. Duplicate keys on insert are a different problem, solved with a unique constraint.

`PUT /envelopes/{id}` can now fail when two people edit at once. `EnvelopeService.UpdateAsync` calls `SaveChangesAsync` bare, so the exception reaches `GlobalExceptionHandler` and becomes a 500 where it should be a 409 with ProblemDetails. This is knowingly deferred: the bus path has retries, the HTTP path does not.

Incrementing on every save raises the conflict rate for metadata-only edits. Two people changing an envelope's description will produce a loser, even though the two operations do not really collide. Accepted, because the alternative is incrementing selectively per column, which reopens the lost-update hole above.

`Version` is a monotonically increasing integer, so it can later serve as a revision number for an HTTP `ETag` if API-level optimistic concurrency is ever needed.

If the spending path ever reaches a load where retries become the bottleneck, `ExecuteUpdateAsync` is still there. Switching to it would reverse this decision, so it would call for a new ADR marking this one Superseded rather than an edit to this file.
