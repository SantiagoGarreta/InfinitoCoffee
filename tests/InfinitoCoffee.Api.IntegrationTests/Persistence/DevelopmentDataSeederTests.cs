using InfinitoCoffee.Domain.Users;
using InfinitoCoffee.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InfinitoCoffee.Api.IntegrationTests.Persistence;

public sealed class DevelopmentDataSeederTests
{
    private const string InitialPassword = "Development-only_password!";

    [Fact]
    public async Task SeedAsync_AddsExpectedSystemUserCategoriesAndProducts()
    {
        using var factory = new SqliteInMemoryDbContextFactory();
        await using var dbContext = factory.CreateDbContext();
        var seeder = CreateSeeder();

        await seeder.SeedAsync(dbContext);

        var systemUser = await dbContext.Users.SingleAsync();
        Assert.Equal("root.admin", systemUser.Username);
        Assert.Equal("ROOT.ADMIN", systemUser.NormalizedUsername);
        Assert.Equal("System Administrator", systemUser.DisplayName);
        Assert.Equal(UserRole.Administrator, systemUser.Role);
        Assert.True(systemUser.IsActive);
        Assert.True(systemUser.IsSystemUser);
        Assert.NotEqual(InitialPassword, systemUser.PasswordHash);
        Assert.Equal(
            PasswordVerificationResult.Success,
            new PasswordHasher<User>().VerifyHashedPassword(
                systemUser,
                systemUser.PasswordHash,
                InitialPassword));
        Assert.Equal(5, await dbContext.ProductCategories.CountAsync());
        Assert.Equal(9, await dbContext.Products.CountAsync());
    }

    [Fact]
    public async Task SeedAsync_WhenRunAgain_PreservesSystemUserAndRemainsIdempotent()
    {
        using var factory = new SqliteInMemoryDbContextFactory();
        await using var dbContext = factory.CreateDbContext();
        await CreateSeeder().SeedAsync(dbContext);
        dbContext.ChangeTracker.Clear();

        var original = await dbContext.Users.AsNoTracking().SingleAsync();
        var changedConfiguration = new InitialSystemUserOptions
        {
            Username = "different.admin",
            DisplayName = "Different Administrator",
            Password = "Different_password!"
        };

        await CreateSeeder(changedConfiguration).SeedAsync(dbContext);
        dbContext.ChangeTracker.Clear();

        var persisted = await dbContext.Users.AsNoTracking().SingleAsync();
        Assert.Equal(original.Id, persisted.Id);
        Assert.Equal(original.Username, persisted.Username);
        Assert.Equal(original.DisplayName, persisted.DisplayName);
        Assert.Equal(original.PasswordHash, persisted.PasswordHash);
        Assert.Equal(1, await dbContext.Users.CountAsync());
        Assert.Equal(5, await dbContext.ProductCategories.CountAsync());
        Assert.Equal(9, await dbContext.Products.CountAsync());
    }

    [Theory]
    [InlineData("Username", "INITIAL_ADMIN_USERNAME")]
    [InlineData("DisplayName", "INITIAL_ADMIN_DISPLAY_NAME")]
    [InlineData("Password", "INITIAL_ADMIN_PASSWORD")]
    public async Task SeedAsync_WhenSystemUserDoesNotExistAndRequiredSettingIsMissing_Fails(
        string missingSetting,
        string expectedEnvironmentVariable)
    {
        using var factory = new SqliteInMemoryDbContextFactory();
        await using var dbContext = factory.CreateDbContext();
        var options = CreateValidOptions();
        options = missingSetting switch
        {
            "Username" => new InitialSystemUserOptions
            {
                DisplayName = options.DisplayName,
                Password = options.Password
            },
            "DisplayName" => new InitialSystemUserOptions
            {
                Username = options.Username,
                Password = options.Password
            },
            _ => new InitialSystemUserOptions
            {
                Username = options.Username,
                DisplayName = options.DisplayName
            }
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateSeeder(options).SeedAsync(dbContext));

        Assert.Contains(expectedEnvironmentVariable, exception.Message);
        Assert.Empty(await dbContext.Users.ToListAsync());
        Assert.Empty(await dbContext.ProductCategories.ToListAsync());
        Assert.Empty(await dbContext.Products.ToListAsync());
    }

    [Fact]
    public async Task SeedAsync_WhenSystemUserExists_DoesNotRequireBootstrapSettings()
    {
        using var factory = new SqliteInMemoryDbContextFactory();
        await using var dbContext = factory.CreateDbContext();
        var systemUser = CreateSystemUser("preserved-root", "preserved-hash");
        dbContext.Users.Add(systemUser);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        await CreateSeeder(new InitialSystemUserOptions()).SeedAsync(dbContext);

        var persisted = await dbContext.Users.AsNoTracking().SingleAsync();
        Assert.Equal(systemUser.Id, persisted.Id);
        Assert.Equal("preserved-root", persisted.Username);
        Assert.Equal("preserved-hash", persisted.PasswordHash);
        Assert.Equal(5, await dbContext.ProductCategories.CountAsync());
        Assert.Equal(9, await dbContext.Products.CountAsync());
    }

    [Theory]
    [InlineData("UPDATE Users SET IsActive = 0 WHERE IsSystemUser = 1")]
    [InlineData("UPDATE Users SET Role = 'Kitchen' WHERE IsSystemUser = 1")]
    public async Task SeedAsync_WhenExistingSystemUserViolatesInvariant_FailsWithoutRepairingIt(
        string corruptingSql)
    {
        using var factory = new SqliteInMemoryDbContextFactory();
        await using var dbContext = factory.CreateDbContext();
        var systemUser = CreateSystemUser("root", "original-hash");
        dbContext.Users.Add(systemUser);
        await dbContext.SaveChangesAsync();
        await dbContext.Database.ExecuteSqlRawAsync(corruptingSql);
        dbContext.ChangeTracker.Clear();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateSeeder(new InitialSystemUserOptions()).SeedAsync(dbContext));

        var persisted = await dbContext.Users.AsNoTracking().SingleAsync();
        Assert.Equal("original-hash", persisted.PasswordHash);
        Assert.True(!persisted.IsActive || persisted.Role != UserRole.Administrator);
        Assert.Empty(await dbContext.ProductCategories.ToListAsync());
        Assert.Empty(await dbContext.Products.ToListAsync());
    }

    private static DevelopmentDataSeeder CreateSeeder(InitialSystemUserOptions? options = null)
    {
        return new DevelopmentDataSeeder(
            new PasswordHasher<User>(),
            Options.Create(options ?? CreateValidOptions()));
    }

    private static InitialSystemUserOptions CreateValidOptions()
    {
        return new InitialSystemUserOptions
        {
            Username = "root.admin",
            DisplayName = "System Administrator",
            Password = InitialPassword
        };
    }

    private static User CreateSystemUser(string username, string passwordHash)
    {
        return User.CreateSystemUser(
            username,
            "System Administrator",
            _ => passwordHash);
    }
}
