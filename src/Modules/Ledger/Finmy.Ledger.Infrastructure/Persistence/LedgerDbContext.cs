using Finmy.Ledger.Domain.Transactions;

using Microsoft.EntityFrameworkCore;

namespace Finmy.Ledger.Infrastructure.Persistence;

public sealed class LedgerDbContext(DbContextOptions<LedgerDbContext> options) : DbContext(options)
{
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<IdempotencyRecord> IdempotencyRecords { get; set; }
    public DbSet<TransactionRequestRecord> TransactionRequests { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("ledger");

        #region Transaction

        modelBuilder.Entity<Transaction>().Property(x => x.Amount).HasPrecision(18, 2);
        modelBuilder.Entity<Transaction>().Property(x => x.Description).HasMaxLength(500);

        modelBuilder.Entity<Transaction>().HasIndex(x => x.EnvelopeId);
        modelBuilder.Entity<Transaction>().HasIndex(x => x.SpaceId);

        #endregion Transaction

        #region IdempotencyRecord

        modelBuilder.Entity<IdempotencyRecord>().HasKey(x => new { x.Key, x.SpaceId });

        modelBuilder.Entity<IdempotencyRecord>().Property(x => x.Key).HasMaxLength(255);
        modelBuilder.Entity<IdempotencyRecord>().Property(x => x.RequestHash).HasMaxLength(64);

        #endregion IdempotencyRecord

        #region TransactionRequestRecord

        modelBuilder.Entity<TransactionRequestRecord>().HasKey(x => x.TransactionId);

        modelBuilder.Entity<TransactionRequestRecord>().Property(x => x.ErrorCode).HasMaxLength(255);
        modelBuilder.Entity<TransactionRequestRecord>().Property(x => x.ErrorDescription).HasMaxLength(1000);

        // The pruning background service (commit 9) sweeps on this column.
        modelBuilder.Entity<TransactionRequestRecord>().HasIndex(x => x.ExpiresAtUtc);

        #endregion TransactionRequestRecord
    }
}
