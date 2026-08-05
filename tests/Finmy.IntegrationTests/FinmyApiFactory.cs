using System.Net.Http.Headers;
using System.Net.Http.Json;

using Finmy.Budgeting.Infrastructure.Persistence;
using Finmy.Identity.Application.Authentication.Dtos;
using Finmy.Identity.Infrastructure.Persistence;
using Finmy.Ledger.Infrastructure.Persistence;

using JasperFx.CommandLine;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Hosting;

using Testcontainers.Minio;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace Finmy.IntegrationTests;

/// <summary>
/// Runs the real host over HTTP against real Postgres, Redis and MinIO containers.
///
/// This is what TECH-DEBT #6 was about. The older PostgresFixture builds a DbContext by
/// hand, so it never sees composition: removing AutoApplyTransactions() or switching a
/// handler to inline invocation left that suite green. Everything here goes through
/// Program.cs, so the outbox, the Wolverine policies and the endpoint wiring are all
/// under test.
/// </summary>
public sealed class FinmyApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    static FinmyApiFactory()
    {
        // Program.cs ends in RunJasperFxCommands(args), which parses the command line and
        // treats anything it does not recognise as a bad command. WebApplicationFactory
        // captures the host by making builder.Build() throw a sentinel exception, and the
        // JasperFx command runner swallows it and prints its own help instead. The symptom
        // is "The server has not been started or no web application was configured".
        // AutoStartHost tells JasperFx to skip command parsing and just run the host.
        JasperFxEnvironment.AutoStartHost = true;
    }

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17.10-alpine").Build();

    private readonly RedisContainer _redis = new RedisBuilder("redis:7.4-alpine").Build();

    private readonly MinioContainer _minio = new MinioBuilder("minio/minio:RELEASE.2025-04-22T22-12-26Z").Build();

    private readonly SemaphoreSlim _authLock = new(1, 1);
    private string? _accessToken;

    /// <summary>
    /// Budgeting and Ledger endpoints require an authenticated user (TECH-DEBT #1), so every
    /// HTTP test needs a token. Registering and logging in through the real endpoints, rather
    /// than minting a JWT by hand from the signing key set in ConfigureWebHost, is worth more:
    /// it exercises the actual auth path the tests otherwise never touch. The token is cached
    /// after the first call and reused, guarded by a semaphore since xUnit can run test methods
    /// in the same class in parallel.
    /// </summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync(CancellationToken cancellationToken = default)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(cancellationToken));

        return client;
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_accessToken is not null)
        {
            return _accessToken;
        }

        await _authLock.WaitAsync(cancellationToken);
        try
        {
            if (_accessToken is not null)
            {
                return _accessToken;
            }

            using var client = CreateClient();
            const string email = "integration-tests@finmy.local";
            const string password = "Integration-Tests-Password-1";

            // Registration fails with a conflict on the second test class to call this, which
            // is fine: the fixture is shared across the whole collection (ApiCollection), so
            // the user only needs to exist once.
            await client.PostAsJsonAsync("/api/v1/identity/register", new RegisterRequest(email, password), cancellationToken);

            var loginResponse = await client.PostAsJsonAsync("/api/v1/identity/login", new LoginRequest(email, password), cancellationToken);
            loginResponse.EnsureSuccessStatusCode();

            var authResult = await loginResponse.Content.ReadFromJsonAsync<AuthResult>(cancellationToken)
                ?? throw new InvalidOperationException("Login response had no body.");

            _accessToken = authResult.AccessToken;

            return _accessToken;
        }
        finally
        {
            _authLock.Release();
        }
    }

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(
            _postgres.StartAsync(),
            _redis.StartAsync(),
            _minio.StartAsync());

        // Schemas have to exist before the host starts. IdentitySeederHostedService reads
        // identity.AspNetRoles from StartAsync, so migrating after the host is up is too
        // late. These contexts are built by hand on purpose: this is fixture setup running
        // before there is a service provider to ask.
        await MigrateAsync();

        // Touching the client is what builds the host. Do it here so a startup failure
        // surfaces as a fixture error naming the cause, not as every test failing at once.
        using var client = CreateClient();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();

        await Task.WhenAll(
            _postgres.DisposeAsync().AsTask(),
            _redis.DisposeAsync().AsTask(),
            _minio.DisposeAsync().AsTask());

        GC.SuppressFinalize(this);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Development keeps Wolverine on Dynamic codegen, which is what the test run has a
        // compiler for. It also matches how the app is exercised by hand.
        builder.UseEnvironment(Environments.Development);

        // Every module throws from ConfigureServices when its connection string is blank,
        // and the tracked appsettings.json ships all three empty, so these have to be in
        // place before service registration runs. UseSetting is the only hook early enough.
        var postgres = _postgres.GetConnectionString();

        builder.UseSetting("ConnectionStrings:IdentityDb", postgres);
        builder.UseSetting("ConnectionStrings:BudgetingDb", postgres);
        builder.UseSetting("ConnectionStrings:LedgerDb", postgres);
        builder.UseSetting("ConnectionStrings:Redis", _redis.GetConnectionString());

        // JwtOptions validates on start and requires at least 32 bytes of key material.
        builder.UseSetting("Jwt:SigningKey", "finmy-integration-test-signing-key-32-bytes-minimum");

        // S3StorageOptions validates every one of these on start.
        builder.UseSetting("Storage:Endpoint", $"http://{_minio.Hostname}:{_minio.GetMappedPublicPort(9000)}");
        builder.UseSetting("Storage:Bucket", "receipts");
        builder.UseSetting("Storage:AccessKey", _minio.GetAccessKey());
        builder.UseSetting("Storage:SecretKey", _minio.GetSecretKey());
        builder.UseSetting("Storage:PresignedUrlLifetimeMinutes", "15");

        // Seeding an admin needs a live Identity store and adds nothing these tests assert on.
        builder.UseSetting("IdentitySeed:IsSeedAdmin", "false");
    }

    /// <summary>
    /// All three modules share one container and keep their own schema, so migrations run in
    /// one pass here rather than from Database.Migrate() at startup.
    /// </summary>
    private async Task MigrateAsync()
    {
        var connectionString = _postgres.GetConnectionString();

        await using var identity = new IdentityDbContext(Options<IdentityDbContext>(connectionString));
        await using var budgeting = new BudgetingDbContext(Options<BudgetingDbContext>(connectionString));
        await using var ledger = new LedgerDbContext(Options<LedgerDbContext>(connectionString));

        await identity.Database.MigrateAsync();
        await budgeting.Database.MigrateAsync();
        await ledger.Database.MigrateAsync();
    }

    /// <summary>
    /// Each module keeps its own __EFMigrationsHistory inside its own schema. Without that
    /// the three of them collide in public, which is the same trap the design-time factories
    /// guard against.
    /// </summary>
    private static DbContextOptions<TContext> Options<TContext>(string connectionString)
        where TContext : DbContext
        => new DbContextOptionsBuilder<TContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(
                HistoryRepository.DefaultTableName,
                SchemaFor<TContext>()))
            .Options;

    private static string SchemaFor<TContext>() where TContext : DbContext => typeof(TContext).Name switch
    {
        nameof(IdentityDbContext) => "identity",
        nameof(BudgetingDbContext) => "budgeting",
        nameof(LedgerDbContext) => "ledger",
        _ => throw new ArgumentException($"No schema mapped for {typeof(TContext).Name}.")
    };
}
