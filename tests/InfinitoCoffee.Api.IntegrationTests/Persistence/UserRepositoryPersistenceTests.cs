using InfinitoCoffee.Domain.Users;
using InfinitoCoffee.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace InfinitoCoffee.Api.IntegrationTests.Persistence;

public sealed class UserRepositoryPersistenceTests : IDisposable
{
    private readonly SqliteInMemoryDbContextFactory _dbContextFactory = new();

    [Fact]
    public async Task AddAndGetByIdAsync_PreservesUserData()
    {
        var user = new User(
            "Cashier.One",
            "María — Caja",
            "  hash-preserved-exactly  ",
            UserRole.Cashier);

        await using (var setupContext = _dbContextFactory.CreateDbContext())
        {
            var repository = new UserRepository(setupContext);
            await repository.AddAsync(user);
            await repository.SaveChangesAsync();
        }

        await using var verificationContext = _dbContextFactory.CreateDbContext();
        var verificationRepository = new UserRepository(verificationContext);

        var persisted = await verificationRepository.GetByIdAsync(user.Id);

        Assert.NotNull(persisted);
        Assert.Equal(user.Id, persisted.Id);
        Assert.Equal("Cashier.One", persisted.Username);
        Assert.Equal("CASHIER.ONE", persisted.NormalizedUsername);
        Assert.Equal("María — Caja", persisted.DisplayName);
        Assert.Equal("  hash-preserved-exactly  ", persisted.PasswordHash);
        Assert.Equal(UserRole.Cashier, persisted.Role);
        Assert.True(persisted.IsActive);
        Assert.False(persisted.IsSystemUser);
    }

    [Fact]
    public async Task SystemUser_PersistsSystemFlag()
    {
        var user = CreateSystemUser("Root.One");

        await using (var setupContext = _dbContextFactory.CreateDbContext())
        {
            var repository = new UserRepository(setupContext);
            await repository.AddAsync(user);
            await repository.SaveChangesAsync();
        }

        await using var verificationContext = _dbContextFactory.CreateDbContext();
        var repositoryForVerification = new UserRepository(verificationContext);

        var persisted = await repositoryForVerification.GetByIdAsync(user.Id);

        Assert.NotNull(persisted);
        Assert.True(persisted.IsSystemUser);
        Assert.True(persisted.IsActive);
        Assert.Equal(UserRole.Administrator, persisted.Role);
    }

    [Fact]
    public async Task TwoSystemUsers_ThrowsDbUpdateException()
    {
        await using var dbContext = _dbContextFactory.CreateDbContext();
        var repository = new UserRepository(dbContext);
        await repository.AddAsync(CreateSystemUser("Root.One"));
        await repository.SaveChangesAsync();
        await repository.AddAsync(CreateSystemUser("Root.Two"));

        await Assert.ThrowsAsync<DbUpdateException>(() => repository.SaveChangesAsync());
    }

    [Fact]
    public async Task MultipleNormalAdministrators_AreAllowed()
    {
        await using var dbContext = _dbContextFactory.CreateDbContext();
        var repository = new UserRepository(dbContext);
        await repository.AddAsync(CreateUser("Admin.One", UserRole.Administrator));
        await repository.AddAsync(CreateUser("Admin.Two", UserRole.Administrator));

        await repository.SaveChangesAsync();

        Assert.Equal(2, await dbContext.Users.CountAsync());
    }

    [Fact]
    public void SystemUserIndex_IsUniqueAndFiltered()
    {
        using var dbContext = _dbContextFactory.CreateDbContext();
        var index = dbContext.Model.FindEntityType(typeof(User))!
            .GetIndexes()
            .Single(candidate => candidate.GetDatabaseName() == "UX_Users_SingleSystemUser");

        Assert.True(index.IsUnique);
        Assert.Equal("[IsSystemUser] = 1", index.GetFilter());
    }

    [Fact]
    public async Task GetByNormalizedUsernameAsync_WithExistingUser_ReturnsCorrectUser()
    {
        await using var dbContext = _dbContextFactory.CreateDbContext();
        var repository = new UserRepository(dbContext);
        var administrator = CreateUser("Admin", UserRole.Administrator);
        var cashier = CreateUser("Cashier", UserRole.Cashier);
        await repository.AddAsync(administrator);
        await repository.AddAsync(cashier);
        await repository.SaveChangesAsync();

        var result = await repository.GetByNormalizedUsernameAsync("ADMIN");

        Assert.NotNull(result);
        Assert.Equal(administrator.Id, result.Id);
    }

    [Fact]
    public async Task GetByNormalizedUsernameAsync_WithUnknownUsername_ReturnsNull()
    {
        await using var dbContext = _dbContextFactory.CreateDbContext();
        var repository = new UserRepository(dbContext);

        var result = await repository.GetByNormalizedUsernameAsync("UNKNOWN");

        Assert.Null(result);
    }

    [Fact]
    public async Task ExistsByNormalizedUsernameAsync_ReturnsTrueAndFalseAsExpected()
    {
        await using var dbContext = _dbContextFactory.CreateDbContext();
        var repository = new UserRepository(dbContext);
        await repository.AddAsync(CreateUser("Kitchen", UserRole.Kitchen));
        await repository.SaveChangesAsync();

        var existingResult = await repository.ExistsByNormalizedUsernameAsync("KITCHEN");
        var missingResult = await repository.ExistsByNormalizedUsernameAsync("CASHIER");

        Assert.True(existingResult);
        Assert.False(missingResult);
    }

    [Fact]
    public async Task NormalizedUsername_Duplicate_ThrowsDbUpdateException()
    {
        await using var dbContext = _dbContextFactory.CreateDbContext();
        var repository = new UserRepository(dbContext);
        var firstUser = CreateUser("Admin", UserRole.Administrator);
        var secondUser = CreateUser("admin", UserRole.Cashier);

        Assert.Equal(firstUser.NormalizedUsername, secondUser.NormalizedUsername);

        await repository.AddAsync(firstUser);
        await repository.SaveChangesAsync();
        await repository.AddAsync(secondUser);

        await Assert.ThrowsAsync<DbUpdateException>(() => repository.SaveChangesAsync());
    }

    [Fact]
    public async Task Role_Persisted_UsesStringRepresentation()
    {
        await using var dbContext = _dbContextFactory.CreateDbContext();
        var repository = new UserRepository(dbContext);
        await repository.AddAsync(CreateUser("Kitchen", UserRole.Kitchen));
        await repository.SaveChangesAsync();

        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT Role FROM Users LIMIT 1";

        var storedRole = await command.ExecuteScalarAsync();

        Assert.Equal("Kitchen", Assert.IsType<string>(storedRole));
    }

    [Fact]
    public async Task DeactivateAndActivate_PersistState()
    {
        var user = CreateUser("Cashier", UserRole.Cashier);

        await using (var setupContext = _dbContextFactory.CreateDbContext())
        {
            var repository = new UserRepository(setupContext);
            await repository.AddAsync(user);
            await repository.SaveChangesAsync();
        }

        await using (var deactivationContext = _dbContextFactory.CreateDbContext())
        {
            var repository = new UserRepository(deactivationContext);
            var persisted = await repository.GetByIdAsync(user.Id);
            Assert.NotNull(persisted);

            persisted.Deactivate();
            await repository.SaveChangesAsync();
        }

        await using (var activationContext = _dbContextFactory.CreateDbContext())
        {
            var repository = new UserRepository(activationContext);
            var persisted = await repository.GetByIdAsync(user.Id);
            Assert.NotNull(persisted);
            Assert.False(persisted.IsActive);

            persisted.Activate();
            await repository.SaveChangesAsync();
        }

        await using var verificationContext = _dbContextFactory.CreateDbContext();
        var verificationRepository = new UserRepository(verificationContext);
        var reactivated = await verificationRepository.GetByIdAsync(user.Id);

        Assert.NotNull(reactivated);
        Assert.True(reactivated.IsActive);
    }

    [Fact]
    public async Task ChangeUsername_PersistsValuesAndUpdatesNormalizedSearches()
    {
        var user = CreateUser("Cashier.One", UserRole.Cashier);

        await using (var setupContext = _dbContextFactory.CreateDbContext())
        {
            var repository = new UserRepository(setupContext);
            await repository.AddAsync(user);
            await repository.SaveChangesAsync();
        }

        await using (var updateContext = _dbContextFactory.CreateDbContext())
        {
            var repository = new UserRepository(updateContext);
            var persisted = await repository.GetByIdAsync(user.Id);
            Assert.NotNull(persisted);

            persisted.ChangeUsername("Cashier.Two");
            await repository.SaveChangesAsync();
        }

        await using var verificationContext = _dbContextFactory.CreateDbContext();
        var verificationRepository = new UserRepository(verificationContext);
        var oldUsernameResult = await verificationRepository.GetByNormalizedUsernameAsync("CASHIER.ONE");
        var updated = await verificationRepository.GetByNormalizedUsernameAsync("CASHIER.TWO");

        Assert.Null(oldUsernameResult);
        Assert.NotNull(updated);
        Assert.Equal("Cashier.Two", updated.Username);
        Assert.Equal("CASHIER.TWO", updated.NormalizedUsername);
    }

    [Fact]
    public async Task ChangeRole_PersistsNewRole()
    {
        var user = CreateUser("Cashier", UserRole.Cashier);

        await using (var setupContext = _dbContextFactory.CreateDbContext())
        {
            var repository = new UserRepository(setupContext);
            await repository.AddAsync(user);
            await repository.SaveChangesAsync();
        }

        await using (var updateContext = _dbContextFactory.CreateDbContext())
        {
            var repository = new UserRepository(updateContext);
            var persisted = await repository.GetByIdAsync(user.Id);
            Assert.NotNull(persisted);

            persisted.ChangeRole(UserRole.Administrator);
            await repository.SaveChangesAsync();
        }

        await using var verificationContext = _dbContextFactory.CreateDbContext();
        var verificationRepository = new UserRepository(verificationContext);
        var updated = await verificationRepository.GetByIdAsync(user.Id);

        Assert.NotNull(updated);
        Assert.Equal(UserRole.Administrator, updated.Role);
    }

    [Fact]
    public async Task RepositoryMethods_PropagateCancellationToken()
    {
        await using var dbContext = _dbContextFactory.CreateDbContext();
        var repository = new UserRepository(dbContext);
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var user = CreateUser("Admin", UserRole.Administrator);

        await repository.AddAsync(user, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        var byId = await repository.GetByIdAsync(user.Id, cancellationToken);
        var byUsername = await repository.GetByNormalizedUsernameAsync("ADMIN", cancellationToken);
        var exists = await repository.ExistsByNormalizedUsernameAsync("ADMIN", cancellationToken);

        Assert.NotNull(byId);
        Assert.NotNull(byUsername);
        Assert.True(exists);
    }

    public void Dispose()
    {
        _dbContextFactory.Dispose();
    }

    private static User CreateUser(string username, UserRole role)
    {
        return new User(username, $"{role} user", $"{username}-hash", role);
    }

    private static User CreateSystemUser(string username)
    {
        return User.CreateSystemUser(
            username,
            "System Administrator",
            _ => $"{username}-hash");
    }
}
