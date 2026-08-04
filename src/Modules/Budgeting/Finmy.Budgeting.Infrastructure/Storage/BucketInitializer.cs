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
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var isBucketExist = await Amazon.S3.Util.AmazonS3Util.DoesS3BucketExistV2Async(s3, options.Value.Bucket);

        if (!isBucketExist)
        {
            await s3.PutBucketAsync(options.Value.Bucket, cancellationToken);
            LogBucketInitialized(logger, options.Value.Bucket);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "S3 bucket initialized: {Bucket}")]
    private static partial void LogBucketInitialized(ILogger logger, string bucket);
}
