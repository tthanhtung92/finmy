namespace Finmy.Ledger.Application.Transactions;

public sealed class TransactionNotFoundException(Guid transactionId) : Exception($"Transaction '{transactionId}' does not exist.")
{
    public Guid TransactionId { get; } = transactionId;
}
