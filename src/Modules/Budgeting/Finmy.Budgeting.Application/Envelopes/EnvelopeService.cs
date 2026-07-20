using Finmy.Budgeting.Application.Abstractions;
using Finmy.Budgeting.Application.Envelopes.Dtos;
using Finmy.Budgeting.Domain.Envelopes;
using Finmy.SharedKernel.Results;

namespace Finmy.Budgeting.Application.Envelopes;

public sealed class EnvelopeService(IEnvelopeRepository envelopeRepository, ICategoryRepository categoryRepository)
{
    public async Task<Result<Guid>> CreateAsync(CreateEnvelopeRequest request, CancellationToken cancellationToken)
    {
        var isCategoryExist = await categoryRepository.ExistsAsync(request.CategoryId, cancellationToken);

        if (!isCategoryExist) 
            return EnvelopeErrors.CategoryNotFound(request.CategoryId);

        var result = Envelope.Create(request.Name, request.Description, request.CategoryId, request.Allocated, request.PeriodStart, request.PeriodEnd);

        if (result.IsFailure) 
            return result.Error;

        await envelopeRepository.AddAsync(result.Value, cancellationToken);
        await envelopeRepository.SaveChangesAsync(cancellationToken);

        return result.Value.Id;
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
}