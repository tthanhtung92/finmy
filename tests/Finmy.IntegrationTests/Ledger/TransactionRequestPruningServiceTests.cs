using Finmy.Ledger.Application.Transactions;
using Finmy.Ledger.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Finmy.IntegrationTests.Ledger;

/// <summary>
/// TECH-DEBT #3: the rest of it. Runs PruneExpiredAsync directly rather than waiting out the
/// service's 12-hour timer.
/// </summary>
[Collection(ApiCollection.Name)]
public class TransactionRequestPruningServiceTests(FinmyApiFactory factory)
{
    [Fact]
    public async Task PruneExpiredAsync_deletes_only_rows_past_their_retention_window()
    {
        var expiredId = Guid.CreateVersion7();
        var freshId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();

            dbContext.TransactionRequests.AddRange(
                new TransactionRequestRecord
                {
                    TransactionId = expiredId,
                    Status = TransactionRequestStatus.Succeeded,
                    CreatedAtUtc = now.AddDays(-8),
                    LastUpdatedAtUtc = now.AddDays(-8),
                    ExpiresAtUtc = now.AddDays(-1)
                },
                new TransactionRequestRecord
                {
                    TransactionId = freshId,
                    Status = TransactionRequestStatus.Succeeded,
                    CreatedAtUtc = now,
                    LastUpdatedAtUtc = now,
                    ExpiresAtUtc = now.AddDays(7)
                });

            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var pruningService = factory.Services.GetRequiredService<TransactionRequestPruningService>();
        var deleted = await pruningService.PruneExpiredAsync(TestContext.Current.CancellationToken);

        deleted.ShouldBeGreaterThanOrEqualTo(1);

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDbContext = verifyScope.ServiceProvider.GetRequiredService<LedgerDbContext>();

        (await verifyDbContext.TransactionRequests.AnyAsync(x => x.TransactionId == expiredId, TestContext.Current.CancellationToken))
            .ShouldBeFalse("the expired row must be gone");

        (await verifyDbContext.TransactionRequests.AnyAsync(x => x.TransactionId == freshId, TestContext.Current.CancellationToken))
            .ShouldBeTrue("the row still inside its retention window must survive the sweep");
    }
}
