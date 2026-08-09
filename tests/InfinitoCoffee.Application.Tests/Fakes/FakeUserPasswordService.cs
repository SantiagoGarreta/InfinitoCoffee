using InfinitoCoffee.Application.Authentication.Contracts;
using InfinitoCoffee.Domain.Users;

namespace InfinitoCoffee.Application.Tests.Fakes;

internal sealed class FakeUserPasswordService : IUserPasswordService
{
    public UserPasswordVerificationResult VerificationResult { get; set; }
        = UserPasswordVerificationResult.Success;

    public string NewPasswordHash { get; set; } = "new-password-hash";

    public int VerifyPasswordCalls { get; private set; }

    public int DummyVerificationCalls { get; private set; }

    public int HashPasswordCalls { get; private set; }

    public string? LastPassword { get; private set; }

    public UserPasswordVerificationResult VerifyPassword(User user, string password)
    {
        VerifyPasswordCalls++;
        LastPassword = password;
        return VerificationResult;
    }

    public void PerformDummyVerification(string password)
    {
        DummyVerificationCalls++;
        LastPassword = password;
    }

    public string HashPassword(User user, string password)
    {
        HashPasswordCalls++;
        LastPassword = password;
        return NewPasswordHash;
    }
}
