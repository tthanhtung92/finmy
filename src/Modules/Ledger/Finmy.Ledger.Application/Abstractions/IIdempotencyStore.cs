using Finmy.Ledger.Application.Transactions;

namespace Finmy.Ledger.Application.Abstractions;

public interface IIdempotencyStore
{
    Task<IdempotencyOutcome?> FindAsync(string key, Guid spaceId, CancellationToken cancellationToken = default);
    Task<bool> TryReserveAsync(string key, Guid spaceId, string requestHash, Guid transactionId, CancellationToken cancellationToken = default);
}
