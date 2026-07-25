using Finmy.Ledger.Domain.Transactions;

namespace Finmy.Ledger.Application.Transactions.Dtos;

public sealed record RecordTransactionRequest(
    Guid SpaceId, 
    Guid EnvelopeId,
    decimal Amount, 
    TransactionDirection Direction,
    DateTimeOffset OccurredOn, 
    string? Description);
