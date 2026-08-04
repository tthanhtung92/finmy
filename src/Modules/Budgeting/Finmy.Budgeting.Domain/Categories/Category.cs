using System.Diagnostics.CodeAnalysis;

namespace Finmy.Budgeting.Domain.Categories;

[SuppressMessage("Major Code Smell", "S1144:Unused private types or members should be removed",
    Justification = "EF Core writes Id, Name and Description through these private setters by reflection when it materialises a row.")]
public sealed class Category
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }

    // EF Core materialises through this constructor. There is no factory method yet:
    // the only row is the seed in BudgetingDbContext.OnModelCreating.
    private Category()
    {
    }
}
