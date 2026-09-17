using InfinitoCoffee.Domain.Users;

namespace InfinitoCoffee.Application.Authentication.Contracts;

public interface IUserPasswordService
{
    UserPasswordVerificationResult VerifyPassword(User user, string password);

    void PerformDummyVerification(string password);

    string HashPassword(User user, string password);
}
