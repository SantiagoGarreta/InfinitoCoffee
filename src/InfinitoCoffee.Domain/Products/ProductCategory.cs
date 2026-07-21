namespace InfinitoCoffee.Domain.Products;

public class ProductCategory
{
    public ProductCategory(string name)
    {
        Rename(name);
        Id = Guid.NewGuid();
        IsActive = true;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    public bool IsActive { get; private set; }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Category name is required.", nameof(name));
        }

        Name = name.Trim();
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }
}
