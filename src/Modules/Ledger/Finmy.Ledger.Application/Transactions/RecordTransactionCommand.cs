using Finmy.Ledger.Domain.Transactions;

namespace Finmy.Ledger.Application.Transactions;

public sealed record RecordTransactionCommand(
    Guid TransactionId, Guid SpaceId, Guid EnvelopeId, 
    decimal Amount, TransactionDirection Direction, 
    DateTimeOffset OccurredOn, string? Description);
