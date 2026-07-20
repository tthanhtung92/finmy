namespace Finmy.Budgeting.Application.Envelopes.Dtos;

public sealed record CreateEnvelopeRequest(string Name, string? Description, Guid CategoryId, decimal Allocated, DateTimeOffset PeriodStart, DateTimeOffset PeriodEnd);