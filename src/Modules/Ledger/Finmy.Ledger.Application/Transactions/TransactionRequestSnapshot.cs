using Finmy.SharedKernel.Results;

namespace Finmy.Ledger.Application.Transactions;

public sealed record TransactionRequestSnapshot(
    Guid TransactionId,
    TransactionRequestStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastUpdatedAt,
    DateTimeOffset ExpiresAt,
    Error? Error);
