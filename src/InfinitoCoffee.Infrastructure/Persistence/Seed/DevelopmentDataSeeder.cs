using InfinitoCoffee.Domain.Products;
using InfinitoCoffee.Domain.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InfinitoCoffee.Infrastructure.Persistence.Seed;

public sealed class DevelopmentDataSeeder
{
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IOptions<InitialSystemUserOptions> _initialSystemUserOptions;

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
        new("Cafes", "Espresso", 110m, 38m, "Shot corto e intenso de espresso.", true),
        new("Cafes", "Americano", 125m, 42m, "Espresso alargado con agua caliente.", true),
        new("Cafes", "Cafe latte", 150m, 58m, "Espresso con leche vaporizada.", true),
        new("Cafes", "Cappuccino", 145m, 56m, "Espresso equilibrado con leche y espuma.", true),
        new("Tes", "Te", 95m, 28m, "Te del dia servido caliente.", true),
        new("Bebidas frias", "Jugo de naranja", 130m, 52m, "Jugo natural servido frio.", true),
        new("Panaderia", "Croissant", 95m, 34m, "Croissant de manteca horneado en el dia.", true),
        new("Panaderia", "Medialuna", 80m, 29m, "Medialuna glaseada tradicional.", true),
        new("Comidas", "Sandwich", 220m, 96m, "Sandwich tostado para almuerzo rapido.", true)
    ];

    public DevelopmentDataSeeder(
        IPasswordHasher<User> passwordHasher,
        IOptions<InitialSystemUserOptions> initialSystemUserOptions)
    {
        _passwordHasher = passwordHasher;
        _initialSystemUserOptions = initialSystemUserOptions;
    }

    public async Task SeedAsync(
        InfinitoCoffeeDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        await SeedSystemUserAsync(dbContext, cancellationToken);
        await SeedCategoriesAsync(dbContext, cancellationToken);
        await SeedProductsAsync(dbContext, cancellationToken);
    }

    private async Task SeedSystemUserAsync(
        InfinitoCoffeeDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var systemUser = await dbContext.Users
            .SingleOrDefaultAsync(user => user.IsSystemUser, cancellationToken);

        if (systemUser is not null)
        {
            EnsureValidSystemUser(systemUser);
            return;
        }

        var options = _initialSystemUserOptions.Value;
        var username = GetRequiredValue(options.Username, "INITIAL_ADMIN_USERNAME");
        var displayName = GetRequiredValue(options.DisplayName, "INITIAL_ADMIN_DISPLAY_NAME");
        var password = GetRequiredValue(options.Password, "INITIAL_ADMIN_PASSWORD");

        var newSystemUser = User.CreateSystemUser(
            username,
            displayName,
            user => _passwordHasher.HashPassword(user, password));

        dbContext.Users.Add(newSystemUser);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            dbContext.Entry(newSystemUser).State = EntityState.Detached;

            systemUser = await dbContext.Users
                .SingleOrDefaultAsync(user => user.IsSystemUser, cancellationToken);

            if (systemUser is null)
            {
                throw;
            }

            EnsureValidSystemUser(systemUser);
        }
    }

    private static string GetRequiredValue(string? value, string environmentVariableName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Required seed configuration '{environmentVariableName}' is missing.");
        }

        return value;
    }

    private static void EnsureValidSystemUser(User systemUser)
    {
        if (!systemUser.IsActive || systemUser.Role != UserRole.Administrator)
        {
            throw new InvalidOperationException(
                "The existing system user must be active and have the Administrator role.");
        }
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
                product = new Product(category.Id, seedProduct.Name, seedProduct.Price, seedProduct.Cost, seedProduct.Description);
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
            product.ChangeCost(seedProduct.Cost);
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
        decimal Cost,
        string Description,
        bool IsActive);
}
