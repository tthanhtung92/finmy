namespace Finmy.Contracts.Budgeting;

public sealed record EnvelopeBalanceChangedEvent(
    Guid TransactionId,
    Guid SpaceId,
    Guid EnvelopeId,
    string Name,
    decimal Amount,
    decimal Allocated,
    decimal Spent,
    decimal Remaining,
    DateTimeOffset PeriodStartUtc,
    DateTimeOffset PeriodEndUtc,
    DateTimeOffset OccurredOnUtc);
