using Finmy.Budgeting.Application.Abstractions;
using Finmy.Budgeting.Domain.Receipts;

using Microsoft.EntityFrameworkCore;

namespace Finmy.Budgeting.Infrastructure.Persistence;

public sealed class ReceiptRepository(BudgetingDbContext dbContext) : IReceiptRepository
{
    public void Add(Receipt receipt)
    {
        dbContext.Receipts.Add(receipt);
    }

    public void Remove(Receipt receipt)
    {
        dbContext.Receipts.Remove(receipt);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<Receipt?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await dbContext.Receipts.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }
}
