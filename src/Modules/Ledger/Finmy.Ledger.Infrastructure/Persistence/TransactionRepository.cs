using Finmy.Ledger.Application.Abstractions;
using Finmy.Ledger.Domain.Transactions;

using Microsoft.EntityFrameworkCore;

namespace Finmy.Ledger.Infrastructure.Persistence;

public sealed class TransactionRepository(LedgerDbContext dbContext) : ITransactionRepository
{
    public void Add(Transaction transaction)
    {
        dbContext.Transactions.Add(transaction);
    }

    public async Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await dbContext.Transactions.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }
}
