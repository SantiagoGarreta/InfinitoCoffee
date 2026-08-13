using InfinitoCoffee.Domain.Users;

namespace InfinitoCoffee.Application.Users.Dtos;

public sealed record UserDto(
    Guid Id,
    string Username,
    string DisplayName,
    UserRole Role,
    bool IsActive,
    bool IsSystemUser);
