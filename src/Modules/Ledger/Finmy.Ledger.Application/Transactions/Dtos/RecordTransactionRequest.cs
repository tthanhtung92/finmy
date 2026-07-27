using Finmy.Contracts.Ledger;
using Finmy.Ledger.Domain.Transactions;

using TransactionDirection = Finmy.Ledger.Domain.Transactions.TransactionDirection;

namespace Finmy.Ledger.Application.Transactions.Dtos;

public sealed record RecordTransactionRequest(
    Guid SpaceId,
    Guid EnvelopeId,
    decimal Amount,
    TransactionDirection Direction,
    DateTimeOffset OccurredOn,
    string? Description);
