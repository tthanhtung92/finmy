using Finmy.Ledger.Domain.Transactions;

using Microsoft.EntityFrameworkCore;

namespace Finmy.Ledger.Infrastructure.Persistence;

public sealed class LedgerDbContext(DbContextOptions<LedgerDbContext> options) : DbContext(options)
{
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<IdempotencyRecord> IdempotencyRecords { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("ledger");

        #region Transaction

        builder.Entity<Transaction>().Property(x => x.Amount).HasPrecision(18, 2);
        builder.Entity<Transaction>().Property(x => x.Description).HasMaxLength(500);

        builder.Entity<Transaction>().HasIndex(x => x.EnvelopeId);
        builder.Entity<Transaction>().HasIndex(x => x.SpaceId);

        #endregion Transaction

        #region IdempotencyRecord

        builder.Entity<IdempotencyRecord>().HasKey(x => new { x.Key, x.SpaceId });

        builder.Entity<IdempotencyRecord>().Property(x => x.Key).HasMaxLength(255);
        builder.Entity<IdempotencyRecord>().Property(x => x.RequestHash).HasMaxLength(64);

        #endregion IdempotencyRecord
    }
}
