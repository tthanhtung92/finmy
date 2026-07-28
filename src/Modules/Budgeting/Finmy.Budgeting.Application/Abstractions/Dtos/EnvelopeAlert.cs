using Finmy.Budgeting.Application.Abstractions.Enum;

namespace Finmy.Budgeting.Application.Abstractions.Dtos;

public record EnvelopeAlert(
    Guid Id, 
    EnvelopeAlertLevel Level, 
    decimal Allocated, 
    decimal Spent, 
    decimal Remaining, 
    decimal? AttemptedAmount);
