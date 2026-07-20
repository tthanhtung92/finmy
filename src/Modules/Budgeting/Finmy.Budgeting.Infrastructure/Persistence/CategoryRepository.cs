using Finmy.Budgeting.Application.Abstractions;

using Microsoft.EntityFrameworkCore;

namespace Finmy.Budgeting.Infrastructure.Persistence;

internal sealed class CategoryRepository(BudgetingDbContext dbContext) : ICategoryRepository
{
    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken)
    {
        return await dbContext.Categories.AnyAsync(c => c.Id == id, cancellationToken);
    }
}
