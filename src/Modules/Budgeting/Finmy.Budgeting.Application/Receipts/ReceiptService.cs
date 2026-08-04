using Finmy.Budgeting.Application.Abstractions;
using Finmy.Budgeting.Application.Caching;
using Finmy.Budgeting.Application.Receipts.Dtos;
using Finmy.Budgeting.Domain.Receipts;
using Finmy.SharedKernel.Results;

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace Finmy.Budgeting.Application.Receipts;

public sealed partial class ReceiptService(
    IReceiptStorage receiptStorage,
    IReceiptRepository receiptRepository,
    ILogger<ReceiptService> logger,
    HybridCache cache)
{
    public async Task<Result<UploadReceiptResponse>> UploadAsync(Stream content, long sizeBytes, string contentType, string originalFileName, CancellationToken cancellationToken)
    {
        var validate = ReceiptFileValidator.Validate(sizeBytes, contentType, content);

        if (validate.IsFailure)
            return validate.Error;

        var uploadedAt = DateTimeOffset.UtcNow;

        var objectKey = ReceiptPolicy.BuildObjectKey(contentType, uploadedAt);

        // Upload to S3
        await receiptStorage.UploadAsync(content, objectKey, contentType, cancellationToken);

        var receipt = Receipt.Create(objectKey, contentType, sizeBytes, originalFileName, uploadedAt);

        if (receipt.IsFailure)
            return receipt.Error;

        receiptRepository.Add(receipt.Value);

        await receiptRepository.SaveChangesAsync(cancellationToken);

        var url = receiptStorage.GetPresignedUrl(objectKey);

        return new UploadReceiptResponse(receipt.Value.Id, url);
    }

    public async Task<Result<string>> GetForServingAsync(Guid id, CancellationToken cancellationToken)
    {
        var cacheKey = $"{BudgetingCachePolicy.ReceiptMetaKeyPrefix}:{id}";

        var meta = await cache.GetOrCreateAsync(
            cacheKey,
            async cancelToken =>
            {
                LogCacheMiss(logger, cacheKey);

                var receipt = await receiptRepository.GetByIdAsync(id, cancelToken);

                return receipt is null ? null : new ReceiptMetadata(receipt.ObjectKey, receipt.ContentType);
            },
            options: BudgetingCachePolicy.ReceiptMetaEntry,
            cancellationToken: cancellationToken
        );

        if (meta is null)
            return ReceiptErrors.NotFound(id);

        var url = receiptStorage.GetPresignedUrl(meta.ObjectKey);

        return url;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Cache MISS {CacheKey}")]
    private static partial void LogCacheMiss(ILogger logger, string cacheKey);
}
