namespace Finmy.Budgeting.Domain.Categories;

public sealed class Category
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }

    private Category()
    {
    }

    private Category(Guid id, string name, string? description)
    {
        Id = id;
        Name = name;
        Description = description;
    }
}
