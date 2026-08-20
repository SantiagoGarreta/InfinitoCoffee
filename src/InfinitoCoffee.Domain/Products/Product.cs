namespace InfinitoCoffee.Domain.Products;

public class Product
{
    private Product()
    {
        Name = string.Empty;
    }

    public Product(Guid categoryId, string name, decimal price, string? description = null)
        : this(categoryId, name, price, 0m, description)
    {
    }

    public Product(Guid categoryId, string name, decimal price, decimal cost, string? description = null)
    {
        if (categoryId == Guid.Empty)
        {
            throw new ArgumentException("CategoryId cannot be empty.", nameof(categoryId));
        }

        Id = Guid.NewGuid();
        CategoryId = categoryId;
        IsActive = true;

        Rename(name);
        ChangePrice(price);
        ChangeCost(cost);
        ChangeDescription(description);
    }

    public Guid Id { get; private set; }

    public Guid CategoryId { get; private set; }

    public string Name { get; private set; } = null!;

    public string? Description { get; private set; }

    public decimal Price { get; private set; }

    public decimal Cost { get; private set; }

    public bool IsActive { get; private set; }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Product name is required.", nameof(name));
        }

        Name = name.Trim();
    }

    public void ChangeDescription(string? description)
    {
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    public void ChangePrice(decimal price)
    {
        if (price < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(price), "Price cannot be negative.");
        }

        Price = price;
    }

    public void ChangeCost(decimal cost)
    {
        if (cost < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cost), "Cost cannot be negative.");
        }

        Cost = cost;
    }

    public void ChangeCategory(Guid categoryId)
    {
        if (categoryId == Guid.Empty)
        {
            throw new ArgumentException("CategoryId cannot be empty.", nameof(categoryId));
        }

        CategoryId = categoryId;
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
