using Finmy.Budgeting.Application.Abstractions;
using Finmy.Budgeting.Application.Envelopes.Dtos;
using Finmy.Budgeting.Domain.Envelopes;

using Microsoft.EntityFrameworkCore;

namespace Finmy.Budgeting.Infrastructure.Persistence;

public sealed class EnvelopeRepository(BudgetingDbContext dbContext) : IEnvelopeRepository
{
    public void Add(Envelope envelope)
    {
        dbContext.Envelopes.Add(envelope);
    }

    public void Remove(Envelope envelope)
    {
        dbContext.Envelopes.Remove(envelope);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<Envelope?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await dbContext.Envelopes.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<Envelope> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = dbContext.Envelopes
            .OrderBy(e => e.PeriodStartUtc)
            .ThenBy(e => e.Id);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<MonthlyCategorySummary>> GetMonthlySummaryAsync(DateTimeOffset monthStartUtc, DateTimeOffset monthEndUtc, CancellationToken cancellationToken)
    {
        var envelopes = dbContext.Envelopes;
        var categories = dbContext.Categories;

        var rawQuery = await envelopes
            .Join(categories, e => e.CategoryId, c => c.Id, (e, c) => new { Envelope = e, c.Id, c.Name })
            .Where(x => x.Envelope.PeriodStartUtc < monthEndUtc && x.Envelope.PeriodEndUtc > monthStartUtc)
            .GroupBy(x => new { x.Id, x.Name })
            .Select(g => new
            {
                CategoryId = g.Key.Id,
                CategoryName = g.Key.Name,
                TotalAllocated = g.Sum(x => x.Envelope.Allocated),
                EnvelopeCount = g.Count()
            })
            .OrderBy(s => s.CategoryName)
            .ToListAsync(cancellationToken);

        var result = rawQuery
            .Select(x => new MonthlyCategorySummary
            (
                CategoryId: x.CategoryId,
                CategoryName: x.CategoryName,
                TotalAllocated: x.TotalAllocated,
                EnvelopeCount: x.EnvelopeCount
            ))
            .ToList();

        return result;
    }
}
