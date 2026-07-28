using System.Collections.Concurrent;

using Finmy.Ledger.Application.Abstractions;
using Finmy.Ledger.Application.Transactions;
using Finmy.SharedKernel.Results;

namespace Finmy.Ledger.Infrastructure.Persistence;

public sealed class InMemoryTransactionStatusStore(TimeProvider timeProvider) : ITransactionRequestStatusStore
{
    private readonly ConcurrentDictionary<Guid, TransactionRequestSnapshot> _snapshot = new();

    public Task MarkPendingAsync(Guid transactionId, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();

        _snapshot[transactionId] = new TransactionRequestSnapshot(transactionId, TransactionRequestStatus.Pending, now, now, null);

        return Task.CompletedTask;
    }

    public Task MarkSucceededAsync(Guid transactionId, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();

        _snapshot[transactionId] = _snapshot.TryGetValue(transactionId, out TransactionRequestSnapshot? snapshot)
            ? new TransactionRequestSnapshot(transactionId, TransactionRequestStatus.Succeeded, snapshot.CreatedAt, now, null)
            : new TransactionRequestSnapshot(transactionId, TransactionRequestStatus.Succeeded, now, now, null);

        return Task.CompletedTask;
    }

    public Task MarkFailedAsync(Guid transactionId, Error error, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();

        _snapshot[transactionId] = _snapshot.TryGetValue(transactionId, out TransactionRequestSnapshot? snapshot)
            ? new TransactionRequestSnapshot(transactionId, TransactionRequestStatus.Failed, snapshot.CreatedAt, now, error)
            : new TransactionRequestSnapshot(transactionId, TransactionRequestStatus.Failed, now, now, error);

        return Task.CompletedTask;
    }

    public Task<TransactionRequestSnapshot?> FindAsync(Guid transactionId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult
        (
            _snapshot.TryGetValue(transactionId, out TransactionRequestSnapshot? snapshot)
            ? snapshot
            : null
        );
    }
}
