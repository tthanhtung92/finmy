using Finmy.Budgeting.Application.Abstractions;
using Finmy.Budgeting.Application.Envelopes.Dtos;
using Finmy.Budgeting.Domain.Envelopes;
using Finmy.SharedKernel.Pagination;
using Finmy.SharedKernel.Results;

using Microsoft.Extensions.Caching.Hybrid;

namespace Finmy.Budgeting.Application.Envelopes;

public sealed class EnvelopeService(
    IEnvelopeRepository envelopeRepository,
    ICategoryRepository categoryRepository,
    HybridCache cache)
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

        return result.Value.Id;
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var envelope = await envelopeRepository.GetByIdAsync(id, cancellationToken);

        if (envelope is null)
            return EnvelopeErrors.NotFound(id);

        envelopeRepository.Remove(envelope);

        await envelopeRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
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
        var cacheKey = $"envelopes:list:p{page}:s{pageSize}";

        var result = await cache.GetOrCreateAsync(
            cacheKey,
            async cancelToken =>
            {
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
            cancellationToken: cancellationToken
        );

        return result;
    }

    public async Task<Result<EnvelopeResponse>> UpdateAsync(Guid id, UpdateEnvelopeRequest request, CancellationToken cancellationToken)
    {
        var envelope = await envelopeRepository.GetByIdAsync(id, cancellationToken);

        if (envelope is null)
            return EnvelopeErrors.NotFound(id);

        var isCategoryExists = await categoryRepository.ExistsAsync(request.CategoryId, cancellationToken);

        if (!isCategoryExists)
            return EnvelopeErrors.CategoryNotFound(request.CategoryId);

        var updateResult = envelope.Update(request.Name, request.Description, request.CategoryId, request.Allocated, request.PeriodStart, request.PeriodEnd);

        if (updateResult.IsFailure)
            return updateResult.Error;

        await envelopeRepository.SaveChangesAsync(cancellationToken);

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
}
