using Amazon.Runtime;
using Amazon.S3;

using Finmy.Budgeting.Application.Abstractions;
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
                // Định tuyến request đến URL tùy chỉnh (Ví dụ: LocalStack hoặc MinIO)
                ServiceURL = options.Endpoint,

                // Bắt buộc cho LocalStack/MinIO: Chuyển URL từ dạng bucket.domain sang domain/bucket
                ForcePathStyle = true,

                // Chỉ định vùng mã hóa chữ ký xác thực (AWS4), không bị ghi đè như RegionEndpoint
                AuthenticationRegion = "us-east-1",

                // SDK chỉ tính checksum khi thao tác thật sự bắt buộc
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
            };

            // Khởi tạo client S3 dưới dạng Singleton để tái sử dụng connection pool
            return new AmazonS3Client(new BasicAWSCredentials(options.AccessKey, options.SecretKey), config);
        });
        services.AddSingleton<IReceiptStorage, S3ReceiptStorage>();
        services.TryAddSingleton(TimeProvider.System);

        // AddScoped
        services.AddScoped<IEnvelopeRepository, EnvelopeRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<EnvelopeService>();
        services.AddScoped<IReceiptRepository, ReceiptRepository>();
        services.AddScoped<ReceiptService>();

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
