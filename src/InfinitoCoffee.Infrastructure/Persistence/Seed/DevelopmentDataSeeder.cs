using InfinitoCoffee.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace InfinitoCoffee.Infrastructure.Persistence.Seed;

public sealed class DevelopmentDataSeeder
{
    private static readonly SeedCategory[] Categories =
    [
        new("Cafes", true),
        new("Tes", true),
        new("Bebidas frias", true),
        new("Panaderia", true),
        new("Comidas", true)
    ];

    private static readonly SeedProduct[] Products =
    [
        new("Cafes", "Espresso", 110m, "Shot corto e intenso de espresso.", true),
        new("Cafes", "Americano", 125m, "Espresso alargado con agua caliente.", true),
        new("Cafes", "Cafe latte", 150m, "Espresso con leche vaporizada.", true),
        new("Cafes", "Cappuccino", 145m, "Espresso equilibrado con leche y espuma.", true),
        new("Tes", "Te", 95m, "Te del dia servido caliente.", true),
        new("Bebidas frias", "Jugo de naranja", 130m, "Jugo natural servido frio.", true),
        new("Panaderia", "Croissant", 95m, "Croissant de manteca horneado en el dia.", true),
        new("Panaderia", "Medialuna", 80m, "Medialuna glaseada tradicional.", true),
        new("Comidas", "Sandwich", 220m, "Sandwich tostado para almuerzo rapido.", true)
    ];

    public async Task SeedAsync(
        InfinitoCoffeeDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        await SeedCategoriesAsync(dbContext, cancellationToken);
        await SeedProductsAsync(dbContext, cancellationToken);
    }

    private static async Task SeedCategoriesAsync(
        InfinitoCoffeeDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var existingCategories = await dbContext.ProductCategories
            .ToListAsync(cancellationToken);

        foreach (var seedCategory in Categories)
        {
            var category = existingCategories.SingleOrDefault(existing =>
                string.Equals(existing.Name, seedCategory.Name, StringComparison.OrdinalIgnoreCase));

            if (category is null)
            {
                category = new ProductCategory(seedCategory.Name);
                if (!seedCategory.IsActive)
                {
                    category.Deactivate();
                }

                dbContext.ProductCategories.Add(category);
                existingCategories.Add(category);
                continue;
            }

            category.Rename(seedCategory.Name);
            if (seedCategory.IsActive)
            {
                category.Activate();
            }
            else
            {
                category.Deactivate();
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedProductsAsync(
        InfinitoCoffeeDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var categoriesByName = await dbContext.ProductCategories
            .ToDictionaryAsync(category => category.Name, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var existingProducts = await dbContext.Products
            .ToListAsync(cancellationToken);

        foreach (var seedProduct in Products)
        {
            var category = categoriesByName[seedProduct.CategoryName];
            var product = existingProducts.SingleOrDefault(existing =>
                existing.CategoryId == category.Id &&
                string.Equals(existing.Name, seedProduct.Name, StringComparison.OrdinalIgnoreCase));

            if (product is null)
            {
                product = new Product(category.Id, seedProduct.Name, seedProduct.Price, seedProduct.Description);
                if (!seedProduct.IsActive)
                {
                    product.Deactivate();
                }

                dbContext.Products.Add(product);
                existingProducts.Add(product);
                continue;
            }

            product.ChangeCategory(category.Id);
            product.Rename(seedProduct.Name);
            product.ChangePrice(seedProduct.Price);
            product.ChangeDescription(seedProduct.Description);

            if (seedProduct.IsActive)
            {
                product.Activate();
            }
            else
            {
                product.Deactivate();
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private sealed record SeedCategory(string Name, bool IsActive);

    private sealed record SeedProduct(
        string CategoryName,
        string Name,
        decimal Price,
        string Description,
        bool IsActive);
}
