using Finmy.Budgeting.Application.Abstractions;

using Microsoft.EntityFrameworkCore;

namespace Finmy.Budgeting.Infrastructure.Persistence;

public sealed class ProcessedTransactionStore(BudgetingDbContext dbContext) : IProcessedTransactionStore
{
    public async Task<bool> WasAppliedAsync(Guid transactionId, CancellationToken cancellationToken = default)
    {
        return await dbContext.ProcessedTransactions.AnyAsync(x => x.TransactionId == transactionId, cancellationToken);
    }

    public void MarkApplied(Guid transactionId, Guid envelopeId, decimal amount, DateTimeOffset appliedAtUtc)
    {
        var processedTransaction = new ProcessedTransaction
        {
            TransactionId = transactionId,
            EnvelopeId = envelopeId,
            Amount = amount,
            AppliedAtUtc = appliedAtUtc
        };

        dbContext.ProcessedTransactions.Add(processedTransaction);
    }
}
