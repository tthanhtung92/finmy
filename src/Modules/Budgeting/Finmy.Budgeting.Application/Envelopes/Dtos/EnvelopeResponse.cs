using System.ComponentModel;

namespace Finmy.Budgeting.Application.Envelopes.Dtos;

[ImmutableObject(true)]
public sealed record EnvelopeResponse(Guid Id, string Name, string? Description, Guid CategoryId, decimal Allocated, DateTimeOffset PeriodStartUtc, DateTimeOffset PeriodEndUtc);