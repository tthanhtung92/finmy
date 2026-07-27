using Finmy.Ledger.Domain.Transactions;

using Microsoft.EntityFrameworkCore;

namespace Finmy.Ledger.Infrastructure.Persistence;

public sealed class LedgerDbContext(DbContextOptions<LedgerDbContext> options) : DbContext(options)
{
    public DbSet<Transaction> Transactions { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("ledger");

        builder.Entity<Transaction>().Property(x => x.Amount).HasPrecision(18, 2);
        builder.Entity<Transaction>().Property(x => x.Description).HasMaxLength(500);

        builder.Entity<Transaction>().HasIndex(x => x.EnvelopeId);
        builder.Entity<Transaction>().HasIndex(x => x.SpaceId);
    }
}
