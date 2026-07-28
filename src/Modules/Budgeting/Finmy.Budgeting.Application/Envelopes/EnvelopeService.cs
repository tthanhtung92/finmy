using Finmy.Budgeting.Application.Abstractions;
using Finmy.Budgeting.Application.Abstractions.Dtos;
using Finmy.Budgeting.Application.Caching;
using Finmy.Budgeting.Application.Envelopes.Dtos;
using Finmy.Budgeting.Domain.Envelopes;
using Finmy.SharedKernel.Pagination;
using Finmy.SharedKernel.Results;

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace Finmy.Budgeting.Application.Envelopes;

public sealed class EnvelopeService(
    IEnvelopeRepository envelopeRepository,
    ICategoryRepository categoryRepository,
    HybridCache cache,
    ILogger<EnvelopeService> logger,
    IEnvelopeRealtimeNotifier realtime,
    IEnvelopeCacheInvalidator envelopeCacheInvalidator
    )
{
    public async Task<Result<Guid>> CreateAsync(CreateEnvelopeRequest request, CancellationToken cancellationToken)
    {
        var isCategoryExist = await categoryRepository.ExistsAsync(request.CategoryId, cancellationToken);

        if (!isCategoryExist)
            return EnvelopeErrors.CategoryNotFound(request.CategoryId);

        var result = Envelope.Create(request.Name, request.Description, request.CategoryId, request.Allocated, request.PeriodStart, request.PeriodEnd);

        if (result.IsFailure)
            return result.Error;

        envelopeRepository.Add(result.Value); // Thao tác trong RAM, hứa thêm trước khi SaveChangesAsync

        await envelopeRepository.SaveChangesAsync(cancellationToken);

        await envelopeCacheInvalidator.InvalidateAsync(result.Value.PeriodStartUtc, result.Value.PeriodEndUtc, cancellationToken);

        var snapshot = new EnvelopeBalanceSnapshot
        (
            result.Value.Id,
            result.Value.Name,
            result.Value.Allocated,
            result.Value.Spent,
            result.Value.Remaining
        );
        await realtime.EnvelopeUpdatedAsync(snapshot, cancellationToken);

        return result.Value.Id;
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var envelope = await envelopeRepository.GetByIdAsync(id, cancellationToken);

        if (envelope is null)
            return EnvelopeErrors.NotFound(id);

        envelopeRepository.Remove(envelope);

        await envelopeRepository.SaveChangesAsync(cancellationToken);

        await envelopeCacheInvalidator.InvalidateAsync(envelope.PeriodStartUtc, envelope.PeriodEndUtc, cancellationToken);

        await realtime.EnvelopeDeletedAsync(id, cancellationToken);

        return Result.Success();
    }

    public async Task<Result<EnvelopeResponse>> UpdateAsync(Guid id, UpdateEnvelopeRequest request, CancellationToken cancellationToken)
    {
        var envelope = await envelopeRepository.GetByIdAsync(id, cancellationToken);

        if (envelope is null)
            return EnvelopeErrors.NotFound(id);

        var isCategoryExists = await categoryRepository.ExistsAsync(request.CategoryId, cancellationToken);

        if (!isCategoryExists)
            return EnvelopeErrors.CategoryNotFound(request.CategoryId);

        var oldPeriodStart = envelope.PeriodStartUtc;
        var oldPeriodEnd = envelope.PeriodEndUtc;

        var updateResult = envelope.Update(request.Name, request.Description, request.CategoryId, request.Allocated, request.PeriodStart, request.PeriodEnd);

        if (updateResult.IsFailure)
            return updateResult.Error;

        await envelopeRepository.SaveChangesAsync(cancellationToken);

        await envelopeCacheInvalidator.InvalidateAsync(oldPeriodStart, oldPeriodEnd, cancellationToken);
        await envelopeCacheInvalidator.InvalidateAsync(envelope.PeriodStartUtc, envelope.PeriodEndUtc, cancellationToken);

        var snapshot = new EnvelopeBalanceSnapshot
        (
            envelope.Id,
            envelope.Name,
            envelope.Allocated,
            envelope.Spent,
            envelope.Remaining
        );
        await realtime.EnvelopeUpdatedAsync(snapshot, cancellationToken);

        return new EnvelopeResponse
        (
            envelope.Id,
            envelope.Name,
            envelope.Description,
            envelope.CategoryId,
            envelope.Allocated,
            envelope.Spent,
            envelope.Remaining,
            envelope.PeriodStartUtc,
            envelope.PeriodEndUtc
        );
    }

    public async Task<Result<EnvelopeResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var envelope = await envelopeRepository.GetByIdAsync(id, cancellationToken);

        if (envelope is null)
            return EnvelopeErrors.NotFound(id);

        return new EnvelopeResponse
        (
            envelope.Id,
            envelope.Name,
            envelope.Description,
            envelope.CategoryId,
            envelope.Allocated,
            envelope.Spent,
            envelope.Remaining,
            envelope.PeriodStartUtc,
            envelope.PeriodEndUtc
        );
    }

    public async Task<PagedResult<EnvelopeResponse>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var cacheKey = $"{BudgetingCachePolicy.EnvelopeListKeyPrefix}:p{page}:s{pageSize}";

        var result = await cache.GetOrCreateAsync(
            cacheKey,
            async cancelToken =>
            {
                logger.LogInformation("Cache MISS {CacheKey}", cacheKey);

                var (items, totalCount) = await envelopeRepository.GetPagedAsync(page, pageSize, cancelToken);

                var mappedItems = items
                   .Select(x => new EnvelopeResponse
                   (
                       x.Id,
                       x.Name,
                       x.Description,
                       x.CategoryId,
                       x.Allocated,
                       x.Spent,
                       x.Remaining,
                       x.PeriodStartUtc,
                       x.PeriodEndUtc
                   ))
                   .ToList();

                return new PagedResult<EnvelopeResponse>(mappedItems, page, pageSize, totalCount);
            },
            options: BudgetingCachePolicy.EnvelopeListEntry,
            tags: [BudgetingCachePolicy.EnvelopeListTag],
            cancellationToken: cancellationToken
        );

        return result;
    }

    public async Task<MonthlySummaryResponse> GetMonthlySummaryAsync(int year, int month, CancellationToken cancellationToken)
    {
        var monthStartUtc = new DateTimeOffset(year, month, 1, 0, 0, 0, TimeSpan.Zero);
        var monthEndUtc = monthStartUtc.AddMonths(1);

        // D2: Bắt cả 2 chữ số của tháng => Tháng "07" thay vì "7"
        var cacheKey = $"{BudgetingCachePolicy.EnvelopeSummaryKeyPrefix}:y{year}:m{month:D2}";

        var result = await cache.GetOrCreateAsync(
            cacheKey,
            async cancelToken =>
            {
                logger.LogInformation("Cache MISS {CacheKey}", cacheKey);

                var listSummary = await envelopeRepository.GetMonthlySummaryAsync(monthStartUtc, monthEndUtc, cancelToken);

                var grandTotalAllocated = listSummary.Sum(x => x.TotalAllocated);
                var grandTotalSpent = listSummary.Sum(x => x.TotalSpent);
                var grandTotalRemaining = listSummary.Sum(x => x.TotalRemaining);

                return new MonthlySummaryResponse
                (
                    year,
                    month,
                    listSummary,
                    grandTotalAllocated,
                    grandTotalSpent,
                    grandTotalRemaining
                );
            },
            options: BudgetingCachePolicy.MonthlySummaryEntry,
            tags: [BudgetingCachePolicy.SummaryTag(year, month)],
            cancellationToken: cancellationToken
        );

        return result;
    }
}
