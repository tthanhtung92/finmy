using Finmy.Ledger.Application.Abstractions;
using Finmy.Ledger.Application.Transactions;
using Finmy.SharedKernel.Results;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Finmy.Ledger.Infrastructure.Persistence;

/// <summary>
/// TECH-DEBT #2's Postgres adapter, replacing InMemoryTransactionStatusStore: durable across
/// restarts and visible across replicas, which was the point.
///
/// What this does NOT fix: Wolverine's codegen constructs its own LedgerDbContext inline from
/// DbContextOptions&lt;LedgerDbContext&gt; inside each generated handler wrapper (see
/// RecordTransactionCommandHandler in `codegen preview` output), rather than resolving one
/// through DI. This store is still DI-resolved with its own, separate LedgerDbContext, so a
/// Mark*Async call from inside a handler (RecordTransactionHandler, TransactionConfirmedHandler,
/// EnvelopeOverspentHandler) commits on its own connection and is not atomic with the handler's
/// main SaveChangesAsync. Sharing one transaction would need the handlers themselves to accept
/// an already-open LedgerDbContext instead of resolving the store as an independent service --
/// a bigger change than swapping the adapter. TECH-DEBT #2's original complaint (lost on
/// restart, invisible across replicas) is fixed; the "reports a state the database never
/// reached" case shrinks from an in-memory-vs-Postgres race to a much narrower Postgres-vs-
/// Postgres one, since both writes are now durable and typically land milliseconds apart.
///
/// The endpoint's own MarkPendingAsync call (TransactionEndpoints.RecordTransactionAsync) is
/// unaffected: it runs in the normal HTTP request DI scope, not inside a Wolverine handler, so
/// there is no competing DbContext and the write is exactly as durable as ADR-0011 requires.
/// </summary>
public sealed class PostgresTransactionStatusStore(
    LedgerDbContext dbContext,
    TimeProvider timeProvider,
    IOptions<TransactionStatusOptions> retentionOptions) : ITransactionRequestStatusStore
{
    public async Task MarkPendingAsync(Guid transactionId, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();

        dbContext.TransactionRequests.Add(new TransactionRequestRecord
        {
            TransactionId = transactionId,
            Status = TransactionRequestStatus.Pending,
            CreatedAtUtc = now,
            LastUpdatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(retentionOptions.Value.RetentionDays)
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task MarkSucceededAsync(Guid transactionId, CancellationToken cancellationToken = default) =>
        UpdateAsync(transactionId, TransactionRequestStatus.Succeeded, error: null, cancellationToken);

    public Task MarkFailedAsync(Guid transactionId, Error error, CancellationToken cancellationToken = default) =>
        UpdateAsync(transactionId, TransactionRequestStatus.Failed, error, cancellationToken);

    public async Task<TransactionRequestSnapshot?> FindAsync(Guid transactionId, CancellationToken cancellationToken = default)
    {
        var record = await dbContext.TransactionRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TransactionId == transactionId, cancellationToken);

        if (record is null)
            return null;

        var error = record.ErrorCode is null
            ? null
            : new Error(record.ErrorCode, record.ErrorDescription ?? string.Empty, record.ErrorType ?? ErrorType.Failure);

        return new TransactionRequestSnapshot(record.TransactionId, record.Status, record.CreatedAtUtc, record.LastUpdatedAtUtc, error);
    }

    private async Task UpdateAsync(Guid transactionId, TransactionRequestStatus status, Error? error, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var record = await dbContext.TransactionRequests.FindAsync([transactionId], cancellationToken);

        if (record is null)
        {
            // Should not happen -- ADR-0011 requires MarkPendingAsync before the message is
            // sent -- but a redelivered message must not crash the handler over a missing row.
            record = new TransactionRequestRecord
            {
                TransactionId = transactionId,
                Status = status,
                CreatedAtUtc = now,
                LastUpdatedAtUtc = now,
                ExpiresAtUtc = now.AddDays(retentionOptions.Value.RetentionDays)
            };
            dbContext.TransactionRequests.Add(record);
        }

        record.Status = status;
        record.LastUpdatedAtUtc = now;
        record.ErrorCode = error?.Code;
        record.ErrorDescription = error?.Description;
        record.ErrorType = error?.Type;

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
