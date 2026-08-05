using Amazon.S3;

using Finmy.Budgeting.Infrastructure.Storage;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Finmy.Api.HealthChecks;

/// <summary>
/// Readiness for the S3/MinIO bucket receipts are uploaded to. GetBucketLocationAsync is the
/// same class of call BucketInitializer already makes at startup, just cheap enough to run on
/// every readiness probe.
/// </summary>
public sealed class S3HealthCheck(IAmazonS3 s3, IOptions<S3StorageOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await s3.GetBucketLocationAsync(options.Value.Bucket, cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Cannot reach the S3/MinIO bucket.", ex);
        }
    }
}
