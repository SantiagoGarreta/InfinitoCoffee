using InfinitoCoffee.Application.Authentication.Contracts;
using InfinitoCoffee.Application.Common.Exceptions;
using InfinitoCoffee.Application.Users.Commands;
using InfinitoCoffee.Application.Users.Contracts;
using InfinitoCoffee.Application.Users.Dtos;
using InfinitoCoffee.Application.Users.Queries;
using InfinitoCoffee.Domain.Users;

namespace InfinitoCoffee.Application.Users.Services;

public sealed class UserAdministrationService
{
    public const int PasswordMaxLength = 256;

    private readonly IUserRepository _userRepository;
    private readonly IUserPasswordService _passwordService;

    public UserAdministrationService(
        IUserRepository userRepository,
        IUserPasswordService passwordService)
    {
        _userRepository = userRepository;
        _passwordService = passwordService;
    }

    public async Task<IReadOnlyCollection<UserDto>> GetUsersAsync(
        CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.GetAllAsync(cancellationToken);

        return users
            .OrderBy(user => user.Username, StringComparer.Ordinal)
            .ThenBy(user => user.Id)
            .Select(MapUser)
            .ToArray();
    }

    public async Task<UserDto> GetUserByIdAsync(
        GetUserByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return MapUser(await GetRequiredUserAsync(query.UserId, cancellationToken));
    }

    public async Task<UserDto> CreateUserAsync(
        CreateUserCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidatePassword(command.Password, nameof(command.Password));

        var normalizedUsername = User.NormalizeUsername(command.Username);
        if (await _userRepository.ExistsByNormalizedUsernameAsync(normalizedUsername, cancellationToken))
        {
            throw UsernameConflict(command.Username);
        }

        var user = User.Create(
            command.Username,
            command.DisplayName,
            command.Role,
            candidate => _passwordService.HashPassword(candidate, command.Password));

        await _userRepository.AddAsync(user, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        return MapUser(user);
    }

    public async Task<UserDto> UpdateUserAsync(
        UpdateUserCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var user = await GetRequiredUserAsync(command.UserId, cancellationToken);
        EnsureNormalUser(user);

        if (!Enum.IsDefined(command.Role))
        {
            throw new ArgumentOutOfRangeException(nameof(command.Role), command.Role, "Role is not valid.");
        }

        if (command.ActingUserId == user.Id && command.Role != user.Role)
        {
            throw new ConflictException("An administrator cannot change their own role.");
        }

        var normalizedUsername = User.NormalizeUsername(command.Username);
        if (normalizedUsername != user.NormalizedUsername
            && await _userRepository.ExistsByNormalizedUsernameAsync(normalizedUsername, cancellationToken))
        {
            throw UsernameConflict(command.Username);
        }

        // Display name is applied first because it is the only remaining operation
        // that can fail after the non-mutating validations above.
        user.ChangeDisplayName(command.DisplayName);
        user.ChangeUsername(command.Username);
        if (command.Role != user.Role)
        {
            user.ChangeRole(command.Role);
        }

        await _userRepository.SaveChangesAsync(cancellationToken);
        return MapUser(user);
    }

    public async Task<UserDto> ActivateUserAsync(
        ActivateUserCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var user = await GetRequiredUserAsync(command.UserId, cancellationToken);
        EnsureNormalUser(user);

        user.Activate();
        await _userRepository.SaveChangesAsync(cancellationToken);
        return MapUser(user);
    }

    public async Task<UserDto> DeactivateUserAsync(
        DeactivateUserCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var user = await GetRequiredUserAsync(command.UserId, cancellationToken);
        EnsureNormalUser(user);

        if (command.ActingUserId == user.Id)
        {
            throw new ConflictException("An administrator cannot deactivate their own account.");
        }

        user.Deactivate();
        await _userRepository.SaveChangesAsync(cancellationToken);
        return MapUser(user);
    }

    public async Task ResetPasswordAsync(
        ResetUserPasswordCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var user = await GetRequiredUserAsync(command.UserId, cancellationToken);
        EnsureNormalUser(user);
        ValidatePassword(command.NewPassword, nameof(command.NewPassword));

        var passwordHash = _passwordService.HashPassword(user, command.NewPassword);
        user.ChangePasswordHash(passwordHash);
        await _userRepository.SaveChangesAsync(cancellationToken);
    }

    private async Task<User> GetRequiredUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("User", userId);
    }

    private static void EnsureNormalUser(User user)
    {
        if (user.IsSystemUser)
        {
            throw new ConflictException(
                "The system user cannot be modified through the user administration API.");
        }
    }

    private static void ValidatePassword(string? password, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Password is required.", parameterName);
        }

        if (password.Length > PasswordMaxLength)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"Password cannot exceed {PasswordMaxLength} characters.");
        }
    }

    private static ConflictException UsernameConflict(string username)
    {
        return new ConflictException($"Username '{username}' is already in use.");
    }

    private static UserDto MapUser(User user)
    {
        return new UserDto(
            user.Id,
            user.Username,
            user.DisplayName,
            user.Role,
            user.IsActive,
            user.IsSystemUser);
    }
}
