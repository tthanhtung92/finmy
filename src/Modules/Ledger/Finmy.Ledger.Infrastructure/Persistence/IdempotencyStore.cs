using Finmy.Ledger.Application.Abstractions;
using Finmy.Ledger.Application.Transactions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Npgsql;

namespace Finmy.Ledger.Infrastructure.Persistence;

public sealed partial class IdempotencyStore(
    LedgerDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<IdempotencyStore> logger) : IIdempotencyStore
{
    public async Task<IdempotencyOutcome?> FindAsync(string key, Guid spaceId, CancellationToken cancellationToken = default)
    {
        return await dbContext.IdempotencyRecords
            .Where(x => x.Key == key && x.SpaceId == spaceId)
            .Select(x => new IdempotencyOutcome
            (
                x.RequestHash,
                x.TransactionId
            ))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> TryReserveAsync(string key, Guid spaceId, string requestHash, Guid transactionId, CancellationToken cancellationToken = default)
    {
        var record = new IdempotencyRecord
        {
            Key = key,
            SpaceId = spaceId,
            RequestHash = requestHash,
            TransactionId = transactionId,
            CreatedAtUtc = timeProvider.GetUtcNow()
        };

        try
        {
            dbContext.IdempotencyRecords.Add(record);
            await dbContext.SaveChangesAsync(cancellationToken);

            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            LogDuplicateRecord(logger, ex, key);

            dbContext.Entry(record).State = EntityState.Detached;

            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Duplicate idempotency record for key '{Key}'.")]
    private static partial void LogDuplicateRecord(ILogger logger, Exception exception, string key);
}
