namespace Finmy.Budgeting.Application.Envelopes.Dtos;

public sealed record EnvelopeResponse(Guid Id, string Name, string? Description, Guid CategoryId, decimal Allocated, DateTimeOffset PeriodStartUtc, DateTimeOffset PeriodEndUtc);