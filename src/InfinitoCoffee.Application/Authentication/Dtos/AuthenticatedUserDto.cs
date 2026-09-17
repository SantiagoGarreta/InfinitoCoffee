using InfinitoCoffee.Domain.Users;

namespace InfinitoCoffee.Application.Authentication.Dtos;

public sealed record AuthenticatedUserDto(
    Guid Id,
    string Username,
    string DisplayName,
    UserRole Role);
