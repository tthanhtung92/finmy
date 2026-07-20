using Finmy.Budgeting.Domain.Categories;
using Finmy.Budgeting.Domain.Envelopes;

using Microsoft.EntityFrameworkCore;

namespace Finmy.Budgeting.Infrastructure.Persistence;

public sealed class BudgetingDbContext(DbContextOptions<BudgetingDbContext> options) : DbContext(options)
{
    public DbSet<Envelope> Envelopes { get; set; }
    public DbSet<Category> Categories { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("budgeting");

        modelBuilder.Entity<Envelope>()
            .Property(p => p.Name)
            .HasMaxLength(200);

        modelBuilder.Entity<Envelope>()
            .Property(p => p.Allocated)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Envelope>()
            .HasIndex(x => x.CategoryId);

        modelBuilder.Entity<Category>()
            .Property(p => p.Name)
            .HasMaxLength(200);

        // Seed Category
        modelBuilder.Entity<Category>()
            .HasData(
                new { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Name = "Ăn uống" },
                new { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Name = "Nhà cửa" }
            );
    }
}
