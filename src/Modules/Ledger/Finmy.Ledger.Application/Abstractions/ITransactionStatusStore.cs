using Finmy.Ledger.Application.Transactions;
using Finmy.SharedKernel.Results;

namespace Finmy.Ledger.Application.Abstractions;

public interface ITransactionStatusStore
{
    Task MarkPendingAsync(Guid transactionId, CancellationToken cancellationToken = default);
    Task MarkSucceededAsync(Guid transactionId, CancellationToken cancellationToken = default);
    Task MarkFailedAsync(Guid transactionId, Error error, CancellationToken cancellationToken = default);
    Task<TransactionStatusSnapshot?> FindAsync(Guid transactionId, CancellationToken cancellationToken = default);
}
