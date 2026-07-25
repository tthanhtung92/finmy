namespace Finmy.Ledger.Application.Transactions.Dtos;

public sealed record TransactionStatusResponse(Guid TransactionId, string Status, DateTimeOffset CreatedAt, DateTimeOffset LastUpdatedAt);
