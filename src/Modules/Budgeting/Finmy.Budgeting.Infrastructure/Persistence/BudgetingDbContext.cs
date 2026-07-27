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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("budgeting");

        #region Envelope

        builder.Entity<Envelope>().Property(x => x.Name).HasMaxLength(200);
        builder.Entity<Envelope>().Property(x => x.Allocated).HasPrecision(18, 2);
        builder.Entity<Envelope>().Property(x => x.Spent).HasPrecision(18, 2);
        builder.Entity<Envelope>().Property(x => x.Version).IsConcurrencyToken();

        builder.Entity<Envelope>().HasIndex(x => x.CategoryId);

        builder.Entity<Envelope>().HasOne<Category>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);

        #endregion Envelope

        #region Category

        builder.Entity<Category>().Property(x => x.Name).HasMaxLength(200);

        // Seed
        builder.Entity<Category>().HasData(new { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Name = "Essentials" });

        #endregion Category

        #region Receipt

        builder.Entity<Receipt>().Property(x => x.ObjectKey).HasMaxLength(200).IsRequired();
        builder.Entity<Receipt>().Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        builder.Entity<Receipt>().Property(x => x.OriginalFileName).HasMaxLength(255);

        builder.Entity<Receipt>().HasIndex(x => x.ObjectKey).IsUnique();

        #endregion Receipt
    }
}
