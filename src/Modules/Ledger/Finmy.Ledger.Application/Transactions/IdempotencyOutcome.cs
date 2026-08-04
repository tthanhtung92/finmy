namespace Finmy.Ledger.Application.Transactions;

public sealed record IdempotencyOutcome(string RequestHash, Guid TransactionId);
