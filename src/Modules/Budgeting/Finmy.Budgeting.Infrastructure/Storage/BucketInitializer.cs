using Amazon.S3;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Finmy.Budgeting.Infrastructure.Storage;

public partial class BucketInitializer(
    IAmazonS3 s3,
    IOptions<S3StorageOptions> options,
    ILogger<BucketInitializer> logger) : IHostedService
{
    private const int MaxAttempts = 5;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // MinIO/S3 can still be starting up behind its own healthcheck delay when this host
        // starts (docker compose's `depends_on: condition: service_healthy` narrows the window
        // but does not close it), so a bare first-attempt failure would take the whole host
        // down over a dependency that is a few seconds late.
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                var isBucketExist = await Amazon.S3.Util.AmazonS3Util.DoesS3BucketExistV2Async(s3, options.Value.Bucket);

                if (!isBucketExist)
                {
                    await s3.PutBucketAsync(options.Value.Bucket, cancellationToken);
                    LogBucketInitialized(logger, options.Value.Bucket);
                }

                return;
            }
            catch (Exception ex) when (attempt < MaxAttempts && ex is not OperationCanceledException)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
                LogBucketInitAttemptFailed(logger, attempt, MaxAttempts, delay, ex);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "S3 bucket initialized: {Bucket}")]
    private static partial void LogBucketInitialized(ILogger logger, string bucket);

    [LoggerMessage(Level = LogLevel.Warning, Message = "S3 bucket init attempt {Attempt}/{MaxAttempts} failed, retrying in {Delay}.")]
    private static partial void LogBucketInitAttemptFailed(ILogger logger, int attempt, int maxAttempts, TimeSpan delay, Exception exception);
}
