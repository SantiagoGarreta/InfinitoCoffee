using InfinitoCoffee.Domain.Products;
using InfinitoCoffee.Domain.Users;
using InfinitoCoffee.Infrastructure.Persistence;
using InfinitoCoffee.Infrastructure.Persistence.Maintenance;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InfinitoCoffee.Api.IntegrationTests.Persistence;

public sealed class SystemUserPasswordResetterTests
{
    private const string OldPassword = "Old_password!";
    private const string NewPassword = "  New_password!  ";

    [Fact]
    public async Task ResetAsync_ChangesOnlyPasswordHashAndPreservesOtherData()
    {
        using var factory = new SqliteInMemoryDbContextFactory();
        await using var dbContext = factory.CreateDbContext();
        var passwordHasher = new PasswordHasher<User>();
        var systemUser = CreateSystemUser(passwordHasher, OldPassword);
        var category = new ProductCategory("Existing category");
        var product = new Product(category.Id, "Existing product", 125m, "Existing description");
        dbContext.AddRange(systemUser, category, product);
        await dbContext.SaveChangesAsync();
        var originalPasswordHash = systemUser.PasswordHash;
        dbContext.ChangeTracker.Clear();

        var resetter = CreateResetter(dbContext, passwordHasher, NewPassword);

        await resetter.ResetAsync();

        dbContext.ChangeTracker.Clear();
        var persistedUser = await dbContext.Users.AsNoTracking().SingleAsync();
        var persistedCategory = await dbContext.ProductCategories.AsNoTracking().SingleAsync();
        var persistedProduct = await dbContext.Products.AsNoTracking().SingleAsync();

        Assert.NotEqual(originalPasswordHash, persistedUser.PasswordHash);
        Assert.Equal(
            PasswordVerificationResult.Success,
            passwordHasher.VerifyHashedPassword(
                persistedUser,
                persistedUser.PasswordHash,
                NewPassword));
        Assert.Equal(
            PasswordVerificationResult.Failed,
            passwordHasher.VerifyHashedPassword(
                persistedUser,
                persistedUser.PasswordHash,
                OldPassword));
        Assert.Equal(
            PasswordVerificationResult.Failed,
            passwordHasher.VerifyHashedPassword(
                persistedUser,
                persistedUser.PasswordHash,
                NewPassword.Trim()));

        Assert.Equal(systemUser.Id, persistedUser.Id);
        Assert.Equal(systemUser.Username, persistedUser.Username);
        Assert.Equal(systemUser.NormalizedUsername, persistedUser.NormalizedUsername);
        Assert.Equal(systemUser.DisplayName, persistedUser.DisplayName);
        Assert.Equal(systemUser.Role, persistedUser.Role);
        Assert.Equal(systemUser.IsActive, persistedUser.IsActive);
        Assert.Equal(systemUser.IsSystemUser, persistedUser.IsSystemUser);
        Assert.Equal(1, await dbContext.Users.CountAsync());

        Assert.Equal(category.Id, persistedCategory.Id);
        Assert.Equal(category.Name, persistedCategory.Name);
        Assert.Equal(category.IsActive, persistedCategory.IsActive);
        Assert.Equal(product.Id, persistedProduct.Id);
        Assert.Equal(product.CategoryId, persistedProduct.CategoryId);
        Assert.Equal(product.Name, persistedProduct.Name);
        Assert.Equal(product.Description, persistedProduct.Description);
        Assert.Equal(product.Price, persistedProduct.Price);
        Assert.Equal(product.IsActive, persistedProduct.IsActive);
    }

    [Fact]
    public async Task ResetAsync_WhenSystemUserDoesNotExist_FailsWithoutCreatingUsersOrChangingProducts()
    {
        using var factory = new SqliteInMemoryDbContextFactory();
        await using var dbContext = factory.CreateDbContext();
        var category = new ProductCategory("Existing category");
        var product = new Product(category.Id, "Existing product", 125m);
        dbContext.AddRange(category, product);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateResetter(dbContext, new PasswordHasher<User>(), NewPassword).ResetAsync());

        Assert.Equal("System user was not found.", exception.Message);
        Assert.Empty(await dbContext.Users.ToListAsync());
        Assert.Equal("Existing category", (await dbContext.ProductCategories.SingleAsync()).Name);
        Assert.Equal("Existing product", (await dbContext.Products.SingleAsync()).Name);
    }

    [Fact]
    public async Task ResetAsync_WhenMoreThanOneSystemUserExists_FailsWithoutChangingHashes()
    {
        using var factory = new SqliteInMemoryDbContextFactory();
        await using var dbContext = factory.CreateDbContext();
        await dbContext.Database.ExecuteSqlRawAsync(
            "DROP INDEX \"UX_Users_SingleSystemUser\"");
        var passwordHasher = new PasswordHasher<User>();
        var firstUser = CreateSystemUser(passwordHasher, OldPassword, "root.one");
        var secondUser = CreateSystemUser(passwordHasher, "Second_old_password!", "root.two");
        dbContext.Users.AddRange(firstUser, secondUser);
        await dbContext.SaveChangesAsync();
        var originalHashes = new[] { firstUser.PasswordHash, secondUser.PasswordHash };
        dbContext.ChangeTracker.Clear();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateResetter(dbContext, passwordHasher, NewPassword).ResetAsync());

        Assert.Equal("More than one system user was found.", exception.Message);
        var persistedHashes = await dbContext.Users
            .OrderBy(user => user.Username)
            .Select(user => user.PasswordHash)
            .ToArrayAsync();
        Assert.Equal(originalHashes, persistedHashes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ResetAsync_WhenNewPasswordIsMissing_FailsWithSafeMessage(string? newPassword)
    {
        using var factory = new SqliteInMemoryDbContextFactory();
        await using var dbContext = factory.CreateDbContext();
        var passwordHasher = new PasswordHasher<User>();
        var systemUser = CreateSystemUser(passwordHasher, OldPassword);
        dbContext.Users.Add(systemUser);
        await dbContext.SaveChangesAsync();
        var originalHash = systemUser.PasswordHash;
        dbContext.ChangeTracker.Clear();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateResetter(dbContext, passwordHasher, newPassword).ResetAsync());

        Assert.Equal(
            "System user reset configuration 'SYSTEM_USER_NEW_PASSWORD' is required.",
            exception.Message);
        Assert.DoesNotContain(OldPassword, exception.Message);
        Assert.DoesNotContain(originalHash, exception.Message);
        Assert.Equal(originalHash, (await dbContext.Users.AsNoTracking().SingleAsync()).PasswordHash);
    }

    [Theory]
    [InlineData("UPDATE Users SET IsActive = 0 WHERE IsSystemUser = 1", "The system user must be active.")]
    [InlineData("UPDATE Users SET Role = 'Kitchen' WHERE IsSystemUser = 1", "The system user must have the Administrator role.")]
    public async Task ResetAsync_WhenSystemUserIsInvalid_FailsWithoutChangingHashOrLeakingSecrets(
        string corruptingSql,
        string expectedMessage)
    {
        using var factory = new SqliteInMemoryDbContextFactory();
        await using var dbContext = factory.CreateDbContext();
        var passwordHasher = new PasswordHasher<User>();
        var systemUser = CreateSystemUser(passwordHasher, OldPassword);
        dbContext.Users.Add(systemUser);
        await dbContext.SaveChangesAsync();
        var originalHash = systemUser.PasswordHash;
        await dbContext.Database.ExecuteSqlRawAsync(corruptingSql);
        dbContext.ChangeTracker.Clear();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateResetter(dbContext, passwordHasher, NewPassword).ResetAsync());

        Assert.Equal(expectedMessage, exception.Message);
        Assert.DoesNotContain(NewPassword, exception.Message);
        Assert.DoesNotContain(originalHash, exception.Message);
        Assert.Equal(originalHash, (await dbContext.Users.AsNoTracking().SingleAsync()).PasswordHash);
    }

    [Fact]
    public async Task ResetAsync_PropagatesCancellationToken()
    {
        using var factory = new SqliteInMemoryDbContextFactory();
        await using var dbContext = factory.CreateDbContext();
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateResetter(dbContext, new PasswordHasher<User>(), NewPassword)
                .ResetAsync(cancellationTokenSource.Token));
    }

    private static SystemUserPasswordResetter CreateResetter(
        InfinitoCoffeeDbContext dbContext,
        IPasswordHasher<User> passwordHasher,
        string? newPassword)
    {
        return new SystemUserPasswordResetter(
            dbContext,
            passwordHasher,
            Options.Create(new SystemUserPasswordResetOptions
            {
                NewPassword = newPassword
            }));
    }

    private static User CreateSystemUser(
        IPasswordHasher<User> passwordHasher,
        string password,
        string username = "root")
    {
        return User.CreateSystemUser(
            username,
            "System Administrator",
            user => passwordHasher.HashPassword(user, password));
    }
}
