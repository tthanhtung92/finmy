using Amazon.Runtime;
using Amazon.S3;

using Finmy.Budgeting.Application.Abstractions;
using Finmy.Budgeting.Application.Caching;
using Finmy.Budgeting.Application.Envelopes;
using Finmy.Budgeting.Application.Receipts;
using Finmy.Budgeting.Infrastructure.Persistence;
using Finmy.Budgeting.Infrastructure.Storage;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using Wolverine.EntityFrameworkCore;

namespace Finmy.Budgeting.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure DbContext
        AddDbContext(services, configuration);

        // Configure Options
        AddOptions(services, configuration);

        // AddSingleton
        services.AddSingleton<IAmazonS3>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<S3StorageOptions>>().Value;

            var config = new AmazonS3Config
            {
                // Route requests to a custom URL (LocalStack or MinIO, for example)
                ServiceURL = options.Endpoint,

                // Required for LocalStack and MinIO: use domain/bucket URLs instead of bucket.domain
                ForcePathStyle = true,

                // Pins the region used for AWS4 signature signing; unlike RegionEndpoint it is not overridden
                AuthenticationRegion = "us-east-1",

                // Have the SDK compute checksums only where the operation actually requires them
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
            };

            // Register the S3 client as a singleton so the connection pool is reused
            return new AmazonS3Client(new BasicAWSCredentials(options.AccessKey, options.SecretKey), config);
        });
        services.AddSingleton<IReceiptStorage, S3ReceiptStorage>();
        services.TryAddSingleton(TimeProvider.System);

        // AddScoped
        services.AddScoped<IEnvelopeRepository, EnvelopeRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IReceiptRepository, ReceiptRepository>();
        services.AddScoped<IEnvelopeCacheInvalidator, EnvelopeCacheInvalidator>();
        services.AddScoped<EnvelopeService>();
        services.AddScoped<ReceiptService>();
        services.AddScoped<IProcessedTransactionStore, ProcessedTransactionStore>();

        // Configure Hosted Service
        services.AddHostedService<BucketInitializer>();

        return services;
    }

    private static void AddDbContext(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("BudgetingDb");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'BudgetingDb' is not configured.");
        }
        services.AddDbContextWithWolverineIntegration<BudgetingDbContext>(options => options.UseNpgsql(connectionString));
    }

    private static void AddOptions(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<S3StorageOptions>()
            .Bind(configuration.GetSection(S3StorageOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Endpoint),
                $"S3StorageOptions {nameof(S3StorageOptions.Endpoint)} is not configured.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Bucket),
                $"S3StorageOptions {nameof(S3StorageOptions.Bucket)} is not configured.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.AccessKey),
                $"S3StorageOptions {nameof(S3StorageOptions.AccessKey)} is not configured.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.SecretKey),
                $"S3StorageOptions {nameof(S3StorageOptions.SecretKey)} is not configured.")
            .Validate(o => o.PresignedUrlLifetimeMinutes > 0,
                $"S3 Storage {nameof(S3StorageOptions.PresignedUrlLifetimeMinutes)} must be > 0.")
            .ValidateOnStart();
    }
}
