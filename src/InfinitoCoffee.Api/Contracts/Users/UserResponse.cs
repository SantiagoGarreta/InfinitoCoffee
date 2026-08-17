using InfinitoCoffee.Domain.Users;

namespace InfinitoCoffee.Api.Contracts.Users;

public sealed record UserResponse(
    Guid Id,
    string Username,
    string DisplayName,
    UserRole Role,
    bool IsActive,
    bool IsSystemUser);
