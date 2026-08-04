using Finmy.Budgeting.Application.Envelopes.Dtos;
using Finmy.Budgeting.Domain.Envelopes;
using Finmy.SharedKernel.Results;

namespace Finmy.Budgeting.Application.Abstractions;

public interface IEnvelopeRepository
{
    void Add(Envelope envelope);
    void Remove(Envelope envelope);

    /// <summary>
    /// Returns <see cref="EnvelopeErrors.ConcurrencyConflict"/> when the concurrency token no
    /// longer matches, rather than throwing. The adapter translates because
    /// DbUpdateConcurrencyException belongs to EF Core, and this layer is not allowed to see
    /// EF Core; that is the whole reason IEnvelopeRepository exists (ADR-0009, ADR-0010).
    /// </summary>
    Task<Result> SaveChangesAsync(CancellationToken cancellationToken);
    Task<Envelope?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<(IReadOnlyList<Envelope> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<IReadOnlyList<MonthlyCategorySummary>> GetMonthlySummaryAsync(DateTimeOffset monthStartUtc, DateTimeOffset monthEndUtc, CancellationToken cancellationToken);
}
