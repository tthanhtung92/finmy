using Finmy.SharedKernel.Results;

namespace Finmy.Ledger.Application.Transactions;

public sealed record TransactionStatusSnapshot(Guid TransactionId, TransactionStatus Status, DateTimeOffset CreatedAt, DateTimeOffset LastUpdatedAt, Error? Error);
