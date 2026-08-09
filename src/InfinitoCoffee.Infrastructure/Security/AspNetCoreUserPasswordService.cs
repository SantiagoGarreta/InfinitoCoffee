using InfinitoCoffee.Application.Authentication.Contracts;
using InfinitoCoffee.Domain.Users;
using Microsoft.AspNetCore.Identity;

namespace InfinitoCoffee.Infrastructure.Security;

public sealed class AspNetCoreUserPasswordService : IUserPasswordService
{
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly User _dummyUser;
    private readonly string _dummyPasswordHash;

    public AspNetCoreUserPasswordService(IPasswordHasher<User> passwordHasher)
    {
        _passwordHasher = passwordHasher;
        _dummyUser = new User(
            "authentication-dummy",
            "Authentication Dummy",
            "dummy-password-hash-placeholder",
            UserRole.Cashier);
        _dummyPasswordHash = _passwordHasher.HashPassword(
            _dummyUser,
            Guid.NewGuid().ToString("N"));
    }

    public UserPasswordVerificationResult VerifyPassword(User user, string password)
    {
        ArgumentNullException.ThrowIfNull(user);

        return MapResult(_passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password));
    }

    public void PerformDummyVerification(string password)
    {
        _ = _passwordHasher.VerifyHashedPassword(
            _dummyUser,
            _dummyPasswordHash,
            password);
    }

    public string HashPassword(User user, string password)
    {
        ArgumentNullException.ThrowIfNull(user);
        return _passwordHasher.HashPassword(user, password);
    }

    private static UserPasswordVerificationResult MapResult(PasswordVerificationResult result)
    {
        return result switch
        {
            PasswordVerificationResult.Success => UserPasswordVerificationResult.Success,
            PasswordVerificationResult.SuccessRehashNeeded => UserPasswordVerificationResult.SuccessRehashNeeded,
            _ => UserPasswordVerificationResult.Failed
        };
    }
}
