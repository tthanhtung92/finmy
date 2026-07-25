using Finmy.SharedKernel.Results;

namespace Finmy.Budgeting.Domain.Receipts;

public sealed class Receipt
{
    public Guid Id { get; private set; }
    public string ObjectKey { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public long SizeBytes { get; private set; }
    public string? OriginalFileName { get; private set; }
    public DateTimeOffset UploadedAtUtc { get; private set; }

    private Receipt()
    {
    }

    private Receipt(
        Guid id, string objectKey, string contentType,
        long sizeBytes, string originalFileName,
        DateTimeOffset uploadedAtUtc)
    {
        Id = id;
        ObjectKey = objectKey;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        OriginalFileName = originalFileName;
        UploadedAtUtc = uploadedAtUtc;
    }

    public static Result<Receipt> Create(
        string objectKey, string contentType,
        long sizeBytes, string originalFileName,
        DateTimeOffset uploadedAtUtc)
    {
        uploadedAtUtc = uploadedAtUtc.ToUniversalTime();

        var validateResult = Validate(objectKey, contentType, sizeBytes);

        if (validateResult.IsFailure)
        {
            return validateResult.Error;
        }

        return new Receipt
        (
            Guid.CreateVersion7(),
            objectKey,
            contentType,
            sizeBytes,
            originalFileName,
            uploadedAtUtc
        );
    }

    private static Result Validate(string objectKey, string contentType, long sizeBytes)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
            return Result.Failure(ReceiptErrors.ObjectKeyRequired);

        if (string.IsNullOrWhiteSpace(contentType))
            return Result.Failure(ReceiptErrors.ContentTypeRequired);

        if (sizeBytes <= 0)
            return Result.Failure(ReceiptErrors.SizeNotPositive);

        return Result.Success();
    }
}
