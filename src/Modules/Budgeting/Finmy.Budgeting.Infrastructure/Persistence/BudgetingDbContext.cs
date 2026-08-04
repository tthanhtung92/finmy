using Finmy.Budgeting.Domain.Categories;
using Finmy.Budgeting.Domain.Envelopes;
using Finmy.Budgeting.Domain.Receipts;

using Microsoft.EntityFrameworkCore;

namespace Finmy.Budgeting.Infrastructure.Persistence;

public sealed class BudgetingDbContext(DbContextOptions<BudgetingDbContext> options) : DbContext(options)
{
    public DbSet<Envelope> Envelopes { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Receipt> Receipts { get; set; }
    public DbSet<ProcessedTransaction> ProcessedTransactions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("budgeting");

        #region Envelope

        modelBuilder.Entity<Envelope>().Property(x => x.Name).HasMaxLength(200);
        modelBuilder.Entity<Envelope>().Property(x => x.Allocated).HasPrecision(18, 2);
        modelBuilder.Entity<Envelope>().Property(x => x.Spent).HasPrecision(18, 2);
        modelBuilder.Entity<Envelope>().Property(x => x.Version).IsConcurrencyToken();

        modelBuilder.Entity<Envelope>().HasIndex(x => x.CategoryId);

        modelBuilder.Entity<Envelope>().HasOne<Category>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);

        #endregion Envelope

        #region Category

        modelBuilder.Entity<Category>().Property(x => x.Name).HasMaxLength(200);

        // Seed
        modelBuilder.Entity<Category>().HasData(new { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Name = "Essentials" });

        #endregion Category

        #region Receipt

        modelBuilder.Entity<Receipt>().Property(x => x.ObjectKey).HasMaxLength(200).IsRequired();
        modelBuilder.Entity<Receipt>().Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        modelBuilder.Entity<Receipt>().Property(x => x.OriginalFileName).HasMaxLength(255);

        modelBuilder.Entity<Receipt>().HasIndex(x => x.ObjectKey).IsUnique();

        #endregion Receipt

        #region ProcessedTransaction

        modelBuilder.Entity<ProcessedTransaction>().Property(x => x.Amount).HasPrecision(18, 2);

        modelBuilder.Entity<ProcessedTransaction>().HasKey(x => x.TransactionId);

        #endregion ProcessedTransaction
    }
}
