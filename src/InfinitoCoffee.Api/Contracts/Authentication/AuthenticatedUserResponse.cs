namespace InfinitoCoffee.Api.Contracts.Authentication;

public sealed record AuthenticatedUserResponse(
    Guid Id,
    string Username,
    string DisplayName,
    string Role);
