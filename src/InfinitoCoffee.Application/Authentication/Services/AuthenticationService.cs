using InfinitoCoffee.Application.Authentication.Commands;
using InfinitoCoffee.Application.Authentication.Contracts;
using InfinitoCoffee.Application.Authentication.Dtos;
using InfinitoCoffee.Application.Authentication.Exceptions;
using InfinitoCoffee.Application.Users.Contracts;
using InfinitoCoffee.Domain.Users;

namespace InfinitoCoffee.Application.Authentication.Services;

public sealed class AuthenticationService
{
    private readonly IUserRepository _userRepository;
    private readonly IUserPasswordService _passwordService;

    public AuthenticationService(
        IUserRepository userRepository,
        IUserPasswordService passwordService)
    {
        _userRepository = userRepository;
        _passwordService = passwordService;
    }

    public async Task<AuthenticatedUserDto> AuthenticateAsync(
        LoginCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var normalizedUsername = User.NormalizeUsername(command.Username);
        var user = await _userRepository.GetByNormalizedUsernameAsync(
            normalizedUsername,
            cancellationToken);

        if (user is null)
        {
            _passwordService.PerformDummyVerification(command.Password);
            throw new InvalidCredentialsException();
        }

        var verificationResult = _passwordService.VerifyPassword(user, command.Password);

        if (!user.IsActive
            || verificationResult is not (
                UserPasswordVerificationResult.Success
                or UserPasswordVerificationResult.SuccessRehashNeeded))
        {
            throw new InvalidCredentialsException();
        }

        if (verificationResult == UserPasswordVerificationResult.SuccessRehashNeeded)
        {
            var newPasswordHash = _passwordService.HashPassword(user, command.Password);
            user.ChangePasswordHash(newPasswordHash);
            await _userRepository.SaveChangesAsync(cancellationToken);
        }

        return new AuthenticatedUserDto(
            user.Id,
            user.Username,
            user.DisplayName,
            user.Role);
    }
}
