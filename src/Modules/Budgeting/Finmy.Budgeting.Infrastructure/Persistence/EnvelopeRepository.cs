using Finmy.Budgeting.Application.Abstractions;
using Finmy.Budgeting.Domain.Envelopes;

using Microsoft.EntityFrameworkCore;

namespace Finmy.Budgeting.Infrastructure.Persistence;

internal sealed class EnvelopeRepository(BudgetingDbContext dbContext) : IEnvelopeRepository
{
    public async Task AddAsync(Envelope envelope, CancellationToken cancellationToken)
    {
        await dbContext.Envelopes.AddAsync(envelope, cancellationToken);
    }

    public async Task<Envelope?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await dbContext.Envelopes.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return await dbContext.SaveChangesAsync(cancellationToken);
    }
}
