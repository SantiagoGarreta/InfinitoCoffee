using InfinitoCoffee.Application.Common.Exceptions;
using InfinitoCoffee.Application.Tests.Fakes;
using InfinitoCoffee.Application.Users.Commands;
using InfinitoCoffee.Application.Users.Queries;
using InfinitoCoffee.Application.Users.Services;
using InfinitoCoffee.Domain.Users;

namespace InfinitoCoffee.Application.Tests.Users;

public sealed class UserAdministrationServiceTests
{
    [Theory]
    [InlineData(UserRole.Administrator)]
    [InlineData(UserRole.Cashier)]
    [InlineData(UserRole.Kitchen)]
    public async Task CreateUserAsync_CreatesActiveNormalUserForEveryRole(UserRole role)
    {
        var (service, repository, passwords) = CreateService();
        var result = await service.CreateUserAsync(new("new.user", "New User", " password preserved ", role));

        Assert.Equal(role, result.Role);
        Assert.True(result.IsActive);
        Assert.False(result.IsSystemUser);
        Assert.Equal(" password preserved ", passwords.LastPassword);
        Assert.Same(repository.Users.Single(), passwords.LastUser);
        Assert.Equal(1, repository.SaveChangesCalls);
    }

    [Fact]
    public async Task CreateUserAsync_WithCaseInsensitiveDuplicate_ThrowsConflictWithoutSaving()
    {
        var (service, repository, _) = CreateService(CreateUser("Juan"));
        await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateUserAsync(new("juan", "Other", "secret", UserRole.Cashier)));
        Assert.Equal(0, repository.SaveChangesCalls);
    }

    [Fact]
    public async Task GetUsersAsync_ReturnsOrderedPublicData()
    {
        var (service, _, _) = CreateService(CreateUser("zeta"), CreateUser("alpha"));
        var users = await service.GetUsersAsync();
        Assert.Equal(["alpha", "zeta"], users.Select(x => x.Username));
    }

    [Fact]
    public async Task GetUserByIdAsync_WithUnknownId_ThrowsNotFound()
    {
        var (service, _, _) = CreateService();
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetUserByIdAsync(new(Guid.NewGuid())));
    }

    [Fact]
    public async Task UpdateUserAsync_UpdatesUsernameNormalizationDisplayAndOtherUsersRole()
    {
        var target = CreateUser("old.name", UserRole.Cashier);
        var (service, repository, _) = CreateService(target);
        var result = await service.UpdateUserAsync(new(target.Id, Guid.NewGuid(), "New.Name", " New Display ", UserRole.Kitchen));
        Assert.Equal("New.Name", result.Username);
        Assert.Equal("NEW.NAME", target.NormalizedUsername);
        Assert.Equal("New Display", result.DisplayName);
        Assert.Equal(UserRole.Kitchen, result.Role);
        Assert.Equal(1, repository.SaveChangesCalls);
    }

    [Fact]
    public async Task UpdateUserAsync_SelfUsernameAndDisplayAreAllowedWhenRoleIsUnchanged()
    {
        var self = CreateUser("admin.one", UserRole.Administrator);
        var (service, repository, _) = CreateService(self);
        var result = await service.UpdateUserAsync(new(self.Id, self.Id, "admin.two", "Admin Two", UserRole.Administrator));
        Assert.Equal("admin.two", result.Username);
        Assert.Equal("Admin Two", result.DisplayName);
        Assert.Equal(1, repository.SaveChangesCalls);
    }

    [Fact]
    public async Task UpdateUserAsync_UnchangedNormalizedUsername_DoesNotConflictWithItself()
    {
        var target = CreateUser("Juan", UserRole.Cashier);
        var (service, repository, _) = CreateService(target);
        var result = await service.UpdateUserAsync(
            new(target.Id, Guid.NewGuid(), "juan", "Updated Juan", UserRole.Cashier));
        Assert.Equal("juan", result.Username);
        Assert.Equal("JUAN", target.NormalizedUsername);
        Assert.Equal(1, repository.SaveChangesCalls);
    }

    [Fact]
    public async Task UpdateUserAsync_OtherUsersNormalizedUsernameConflict_IsRejectedWithoutSaving()
    {
        var existing = CreateUser("Juan", UserRole.Cashier);
        var target = CreateUser("Pedro", UserRole.Kitchen);
        var (service, repository, _) = CreateService(existing, target);
        await Assert.ThrowsAsync<ConflictException>(() =>
            service.UpdateUserAsync(new(target.Id, Guid.NewGuid(), "juan", target.DisplayName, target.Role)));
        Assert.Equal("Pedro", target.Username);
        Assert.Equal(0, repository.SaveChangesCalls);
    }

    [Fact]
    public async Task UpdateUserAsync_SelfRoleChangeIsRejectedWithoutSaving()
    {
        var self = CreateUser("admin.one", UserRole.Administrator);
        var (service, repository, _) = CreateService(self);
        await Assert.ThrowsAsync<ConflictException>(() =>
            service.UpdateUserAsync(new(self.Id, self.Id, self.Username, self.DisplayName, UserRole.Cashier)));
        Assert.Equal(UserRole.Administrator, self.Role);
        Assert.Equal(0, repository.SaveChangesCalls);
    }

    [Fact]
    public async Task ActivateAndDeactivateOtherUser_AreIdempotent()
    {
        var target = CreateUser("cashier", UserRole.Cashier);
        var (service, _, _) = CreateService(target);
        await service.DeactivateUserAsync(new(target.Id, Guid.NewGuid()));
        await service.DeactivateUserAsync(new(target.Id, Guid.NewGuid()));
        Assert.False(target.IsActive);
        await service.ActivateUserAsync(new(target.Id));
        await service.ActivateUserAsync(new(target.Id));
        Assert.True(target.IsActive);
    }

    [Fact]
    public async Task DeactivateUserAsync_SelfIsRejectedWithoutSaving()
    {
        var self = CreateUser("admin", UserRole.Administrator);
        var (service, repository, _) = CreateService(self);
        await Assert.ThrowsAsync<ConflictException>(() => service.DeactivateUserAsync(new(self.Id, self.Id)));
        Assert.True(self.IsActive);
        Assert.Equal(0, repository.SaveChangesCalls);
    }

    [Fact]
    public async Task ResetPasswordAsync_SelfOrOtherNormalUser_ChangesHash()
    {
        var target = CreateUser("admin", UserRole.Administrator);
        var (service, repository, passwords) = CreateService(target);
        await service.ResetPasswordAsync(new(target.Id, " new password "));
        Assert.Equal(passwords.NewPasswordHash, target.PasswordHash);
        Assert.Equal(" new password ", passwords.LastPassword);
        Assert.Equal(1, repository.SaveChangesCalls);
    }

    [Fact]
    public async Task EverySystemUserMutation_IsRejectedWithoutSaving()
    {
        var system = CreateSystemUser();
        var (service, repository, _) = CreateService(system);
        await Assert.ThrowsAsync<ConflictException>(() => service.UpdateUserAsync(new(system.Id, Guid.NewGuid(), "root", "Root", UserRole.Administrator)));
        await Assert.ThrowsAsync<ConflictException>(() => service.ActivateUserAsync(new(system.Id)));
        await Assert.ThrowsAsync<ConflictException>(() => service.DeactivateUserAsync(new(system.Id, Guid.NewGuid())));
        await Assert.ThrowsAsync<ConflictException>(() => service.ResetPasswordAsync(new(system.Id, "password")));
        Assert.Equal(0, repository.SaveChangesCalls);
    }

    [Fact]
    public async Task Operations_PropagateCancellationToken()
    {
        var target = CreateUser();
        var (service, repository, _) = CreateService(target);
        using var source = new CancellationTokenSource();
        await service.GetUserByIdAsync(new GetUserByIdQuery(target.Id), source.Token);
        Assert.Equal(source.Token, repository.LastCancellationToken);
    }

    private static (UserAdministrationService Service, FakeUserRepository Repository, FakeUserPasswordService Passwords) CreateService(params User[] users)
    {
        var repository = new FakeUserRepository();
        repository.Seed(users);
        var passwords = new FakeUserPasswordService();
        return (new UserAdministrationService(repository, passwords), repository, passwords);
    }

    private static User CreateUser(string username = "normal.user", UserRole role = UserRole.Cashier) =>
        User.Create(username, "Normal User", role, _ => "old-hash");

    private static User CreateSystemUser() =>
        User.CreateSystemUser("root", "System Administrator", _ => "system-hash");
}
