using InfinitoCoffee.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;

namespace InfinitoCoffee.Api.IntegrationTests.Persistence;

public sealed class DevelopmentDataSeederTests
{
    [Fact]
    public async Task SeedAsync_AddsTheExpectedCategoriesAndProducts()
    {
        using var factory = new SqliteInMemoryDbContextFactory();
        await using var dbContext = factory.CreateDbContext();
        var seeder = new DevelopmentDataSeeder();

        await seeder.SeedAsync(dbContext);

        var categoriesCount = await dbContext.ProductCategories.CountAsync();
        var productsCount = await dbContext.Products.CountAsync();

        Assert.Equal(5, categoriesCount);
        Assert.Equal(9, productsCount);
    }

    [Fact]
    public async Task SeedAsync_IsIdempotent()
    {
        using var factory = new SqliteInMemoryDbContextFactory();
        await using var dbContext = factory.CreateDbContext();
        var seeder = new DevelopmentDataSeeder();

        await seeder.SeedAsync(dbContext);
        await seeder.SeedAsync(dbContext);

        var categoriesCount = await dbContext.ProductCategories.CountAsync();
        var productsCount = await dbContext.Products.CountAsync();

        Assert.Equal(5, categoriesCount);
        Assert.Equal(9, productsCount);
    }
}
