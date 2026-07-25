using System.Collections.Concurrent;

using Finmy.Ledger.Application.Abstractions;
using Finmy.Ledger.Application.Transactions;
using Finmy.SharedKernel.Results;

namespace Finmy.Ledger.Infrastructure.Persistence;

public sealed class InMemoryTransactionStatusStore(TimeProvider timeProvider) : ITransactionStatusStore
{
    private readonly ConcurrentDictionary<Guid, TransactionStatusSnapshot> _snapshot = new();

    public Task MarkPendingAsync(Guid transactionId, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();

        _snapshot[transactionId] = new TransactionStatusSnapshot(transactionId, TransactionStatus.Pending, now, now, null);

        return Task.CompletedTask;
    }

    public Task MarkSucceededAsync(Guid transactionId, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();

        _snapshot[transactionId] = _snapshot.TryGetValue(transactionId, out TransactionStatusSnapshot? snapshot)
            ? new TransactionStatusSnapshot(transactionId, TransactionStatus.Succeeded, snapshot.CreatedAt, now, null)
            : new TransactionStatusSnapshot(transactionId, TransactionStatus.Succeeded, now, now, null);

        return Task.CompletedTask;
    }

    public Task MarkFailedAsync(Guid transactionId, Error error, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();

        _snapshot[transactionId] = _snapshot.TryGetValue(transactionId, out TransactionStatusSnapshot? snapshot)
            ? new TransactionStatusSnapshot(transactionId, TransactionStatus.Failed, snapshot.CreatedAt, now, error)
            : new TransactionStatusSnapshot(transactionId, TransactionStatus.Failed, now, now, error);

        return Task.CompletedTask;
    }

    public Task<TransactionStatusSnapshot?> FindAsync(Guid transactionId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult
        (
            _snapshot.TryGetValue(transactionId, out TransactionStatusSnapshot? snapshot)
            ? snapshot
            : null
        );
    }
}
