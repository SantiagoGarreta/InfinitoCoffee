using InfinitoCoffee.Domain.Users;

namespace InfinitoCoffee.Application.Users.Commands;

public sealed record UpdateUserCommand(
    Guid UserId,
    Guid ActingUserId,
    string Username,
    string DisplayName,
    UserRole Role);
