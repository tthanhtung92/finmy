using Finmy.Budgeting.Application.Abstractions;
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
    ILogger<EnvelopeService> logger)
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

        await InvalidateAsync(BudgetingCachePolicy.SummaryTagsForPeriod(result.Value.PeriodStartUtc, result.Value.PeriodEndUtc), cancellationToken);

        return result.Value.Id;
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var envelope = await envelopeRepository.GetByIdAsync(id, cancellationToken);

        if (envelope is null)
            return EnvelopeErrors.NotFound(id);

        envelopeRepository.Remove(envelope);

        await envelopeRepository.SaveChangesAsync(cancellationToken);

        await InvalidateAsync(BudgetingCachePolicy.SummaryTagsForPeriod(envelope.PeriodStartUtc, envelope.PeriodEndUtc), cancellationToken);

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

        var oldTags = BudgetingCachePolicy.SummaryTagsForPeriod(oldPeriodStart, oldPeriodEnd);
        var newTags = BudgetingCachePolicy.SummaryTagsForPeriod(envelope.PeriodStartUtc, envelope.PeriodEndUtc);

        //Gộp tag lại, khử trùng lặp bằng HashSet
        HashSet<string> summaryTags = oldTags.ToHashSet();
        summaryTags.UnionWith(newTags);

        await InvalidateAsync(summaryTags.ToList(), cancellationToken);

        return new EnvelopeResponse
        (
            envelope.Id,
            envelope.Name,
            envelope.Description,
            envelope.CategoryId,
            envelope.Allocated,
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
                       x.PeriodStartUtc,
                       x.PeriodEndUtc
                   ))
                   .ToList();

                return new PagedResult<EnvelopeResponse>(mappedItems, page, pageSize, totalCount);
            },
            options: BudgetingCachePolicy.EnvelopeListEntry,
            tags: [ BudgetingCachePolicy.EnvelopeListTag ],
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

                var grandTotal = listSummary.Sum(x => x.TotalAllocated);

                return new MonthlySummaryResponse(year, month, listSummary, grandTotal);
            },
            options: BudgetingCachePolicy.MonthlySummaryEntry,
            tags: [ BudgetingCachePolicy.SummaryTag(year, month) ],
            cancellationToken: cancellationToken
        );

        return result;
    }

    private ValueTask InvalidateAsync(IReadOnlyList<string> summaryTags, CancellationToken cancellationToken)
    {
        string[] tags = [BudgetingCachePolicy.EnvelopeListTag, .. summaryTags];
        return cache.RemoveByTagAsync(tags, cancellationToken);
    }
}
