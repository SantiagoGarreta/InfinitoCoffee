using InfinitoCoffee.Domain.Users;

namespace InfinitoCoffee.Application.Users.Commands;

public sealed record CreateUserCommand(
    string Username,
    string DisplayName,
    string Password,
    UserRole Role);
