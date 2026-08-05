using Finmy.Budgeting.Application.Abstractions;
using Finmy.Budgeting.Application.Envelopes.Dtos;
using Finmy.Budgeting.Domain.Envelopes;
using Finmy.SharedKernel.Observability;
using Finmy.SharedKernel.Results;

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

    public async Task<Result> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            // EF Core puts the concurrency token in the WHERE clause of every UPDATE and
            // DELETE, so this fires whenever another writer got there first. Translating it
            // here keeps EF Core out of the Application layer and turns a 500 into a 409.
            // Note: this only covers the HTTP CRUD path. On the message path
            // AutoApplyTransactions() saves after the handler returns, so that conflict
            // escapes to Wolverine's own retry policy in Program.cs and never reaches here.
            FinmyTelemetry.ConcurrencyConflicts.Add(1);
            return EnvelopeErrors.ConcurrencyConflict;
        }
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
                TotalSpent = g.Sum(x => x.Envelope.Spent),
                TotalRemaining = g.Sum(x => x.Envelope.Allocated - x.Envelope.Spent),
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
                TotalSpent: x.TotalSpent,
                TotalRemaining: x.TotalRemaining,
                EnvelopeCount: x.EnvelopeCount
            ))
            .ToList();

        return result;
    }
}
