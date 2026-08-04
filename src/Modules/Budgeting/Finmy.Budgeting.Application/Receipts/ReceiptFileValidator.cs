using Finmy.SharedKernel.Results;

namespace Finmy.Budgeting.Application.Receipts;

public static class ReceiptFileValidator
{
    public static Result Validate(long sizeBytes, string contentType, Stream content)
    {
        if (sizeBytes <= 0)
        {
            return ReceiptUploadErrors.Empty;
        }

        if (sizeBytes > ReceiptPolicy.MaxSizeBytes)
        {
            return ReceiptUploadErrors.TooLarge(ReceiptPolicy.MaxSizeBytes);
        }

        if (!ReceiptPolicy.IsAllowedContentType(contentType))
        {
            return ReceiptUploadErrors.ContentTypeNotAllowed(contentType);
        }

        Span<byte> header = stackalloc byte[8];
        int read = ReadHeader(content, header);

        if (!ReceiptPolicy.MatchesMagicBytes(contentType, header[..read]))
        {
            return ReceiptUploadErrors.ContentMismatch;
        }

        return Result.Success();
    }

    private static int ReadHeader(Stream content, Span<byte> buffer)
    {
        if (content.CanSeek)
            content.Position = 0;

        int total = 0;
        while (total < buffer.Length)
        {
            int n = content.Read(buffer[total..]);
            if (n == 0) break; // end of stream (file shorter than 8 bytes)
            total += n;
        }

        if (content.CanSeek)
            content.Position = 0; // rewind so the next layer can still read the stream

        return total;
    }
}
