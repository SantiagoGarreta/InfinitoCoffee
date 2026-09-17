using InfinitoCoffee.Application.Authentication.Commands;
using InfinitoCoffee.Application.Authentication.Contracts;
using InfinitoCoffee.Application.Authentication.Exceptions;
using InfinitoCoffee.Application.Authentication.Services;
using InfinitoCoffee.Application.Tests.Fakes;
using InfinitoCoffee.Domain.Users;

namespace InfinitoCoffee.Application.Tests.Authentication;

public sealed class AuthenticationServiceTests
{
    [Fact]
    public async Task AuthenticateAsync_WithValidCredentials_ReturnsPublicUserData()
    {
        var user = CreateUser();
        var repository = new FakeUserRepository();
        repository.Seed(user);
        var passwordService = new FakeUserPasswordService();
        var service = new AuthenticationService(repository, passwordService);

        var result = await service.AuthenticateAsync(
            new LoginCommand("  ADMIN.ONE  ", "correct-password"));

        Assert.Equal(user.Id, result.Id);
        Assert.Equal(user.Username, result.Username);
        Assert.Equal(user.DisplayName, result.DisplayName);
        Assert.Equal(user.Role, result.Role);
        Assert.Equal(1, passwordService.VerifyPasswordCalls);
        Assert.Equal("correct-password", passwordService.LastPassword);
        Assert.Equal(0, repository.SaveChangesCalls);
    }

    [Fact]
    public async Task AuthenticateAsync_WithUnknownUsername_PerformsDummyVerificationAndThrowsGenericError()
    {
        var passwordService = new FakeUserPasswordService();
        var service = new AuthenticationService(new FakeUserRepository(), passwordService);

        var exception = await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            service.AuthenticateAsync(new LoginCommand("unknown", "secret")));

        Assert.Equal(InvalidCredentialsException.DefaultMessage, exception.Message);
        Assert.Equal(1, passwordService.DummyVerificationCalls);
        Assert.Equal(0, passwordService.VerifyPasswordCalls);
    }

    [Fact]
    public async Task AuthenticateAsync_WithWrongPassword_ThrowsSameGenericError()
    {
        var repository = new FakeUserRepository();
        repository.Seed(CreateUser());
        var passwordService = new FakeUserPasswordService
        {
            VerificationResult = UserPasswordVerificationResult.Failed
        };
        var service = new AuthenticationService(repository, passwordService);

        var exception = await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            service.AuthenticateAsync(new LoginCommand("admin.one", "wrong")));

        Assert.Equal(InvalidCredentialsException.DefaultMessage, exception.Message);
        Assert.Equal(1, passwordService.VerifyPasswordCalls);
    }

    [Fact]
    public async Task AuthenticateAsync_WithUnknownVerificationResult_FailsClosed()
    {
        var repository = new FakeUserRepository();
        repository.Seed(CreateUser());
        var passwordService = new FakeUserPasswordService
        {
            VerificationResult = (UserPasswordVerificationResult)999
        };
        var service = new AuthenticationService(repository, passwordService);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            service.AuthenticateAsync(new LoginCommand("admin.one", "password")));
    }

    [Fact]
    public async Task AuthenticateAsync_WithInactiveUser_VerifiesPasswordAndThrowsSameGenericError()
    {
        var user = CreateUser();
        user.Deactivate();
        var repository = new FakeUserRepository();
        repository.Seed(user);
        var passwordService = new FakeUserPasswordService();
        var service = new AuthenticationService(repository, passwordService);

        var exception = await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            service.AuthenticateAsync(new LoginCommand("admin.one", "correct-password")));

        Assert.Equal(InvalidCredentialsException.DefaultMessage, exception.Message);
        Assert.Equal(1, passwordService.VerifyPasswordCalls);
        Assert.Equal(0, repository.SaveChangesCalls);
    }

    [Fact]
    public async Task AuthenticateAsync_WhenRehashIsNeeded_UpdatesHashAndSaves()
    {
        var user = CreateUser();
        var repository = new FakeUserRepository();
        repository.Seed(user);
        var passwordService = new FakeUserPasswordService
        {
            VerificationResult = UserPasswordVerificationResult.SuccessRehashNeeded,
            NewPasswordHash = "updated-password-hash"
        };
        var service = new AuthenticationService(repository, passwordService);

        await service.AuthenticateAsync(
            new LoginCommand("admin.one", "correct-password"));

        Assert.Equal("updated-password-hash", user.PasswordHash);
        Assert.Equal(1, passwordService.HashPasswordCalls);
        Assert.Equal(1, repository.SaveChangesCalls);
    }

    [Fact]
    public async Task AuthenticateAsync_WhenRehashSaveFails_PropagatesFailure()
    {
        var repository = new FakeUserRepository
        {
            SaveChangesException = new InvalidOperationException("Persistence failure")
        };
        repository.Seed(CreateUser());
        var passwordService = new FakeUserPasswordService
        {
            VerificationResult = UserPasswordVerificationResult.SuccessRehashNeeded
        };
        var service = new AuthenticationService(repository, passwordService);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AuthenticateAsync(new LoginCommand("admin.one", "correct-password")));

        Assert.Equal("Persistence failure", exception.Message);
        Assert.Equal(1, repository.SaveChangesCalls);
    }

    [Fact]
    public async Task AuthenticateAsync_PropagatesCancellationToken()
    {
        var service = new AuthenticationService(
            new FakeUserRepository(),
            new FakeUserPasswordService());
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.AuthenticateAsync(
                new LoginCommand("admin.one", "password"),
                cancellationTokenSource.Token));
    }

    private static User CreateUser()
    {
        return new User(
            "Admin.One",
            "Primary Administrator",
            "current-password-hash",
            UserRole.Administrator);
    }
}
