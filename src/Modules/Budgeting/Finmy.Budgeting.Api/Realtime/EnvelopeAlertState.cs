using Finmy.Budgeting.Application.Abstractions.Enum;

namespace Finmy.Budgeting.Api.Realtime;

public record EnvelopeAlertState(
    Guid Id,
    EnvelopeAlertLevel Level,
    decimal Allocated,
    decimal Spent,
    decimal Remaining,
    decimal? AttemptedAmount);