using Finmy.Ledger.Application.Abstractions;
using Finmy.Ledger.Domain.Transactions;

namespace Finmy.Ledger.Infrastructure.Persistence;

public sealed class TransactionRepository(LedgerDbContext dbContext) : ITransactionRepository
{
    public void Add(Transaction transaction)
    {
        dbContext.Transactions.Add(transaction);
    }
}
