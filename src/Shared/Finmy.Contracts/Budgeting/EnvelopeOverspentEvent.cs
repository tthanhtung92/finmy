namespace Finmy.Contracts.Budgeting;

public sealed record EnvelopeOverspentEvent(
    Guid TransactionId,
    Guid SpaceId,
    Guid EnvelopeId,
    decimal AttemptedAmount,
    decimal Allocated,
    decimal Spent,
    decimal Remaining,
    DateTimeOffset DetectedAtUtc);
