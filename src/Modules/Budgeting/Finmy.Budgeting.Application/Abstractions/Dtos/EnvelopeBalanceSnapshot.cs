namespace Finmy.Budgeting.Application.Abstractions.Dtos;

public record EnvelopeBalanceSnapshot(
    Guid Id, 
    string Name, 
    decimal Allocated, 
    decimal Spent, 
    decimal Remaining);
