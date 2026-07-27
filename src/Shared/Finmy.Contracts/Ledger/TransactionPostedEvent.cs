namespace Finmy.Contracts.Ledger;

public sealed record TransactionPostedEvent(Guid TransactionId, Guid SpaceId, Guid EnvelopeId, decimal Amount, TransactionDirection Direction, DateTimeOffset OccurredOnUtc);
