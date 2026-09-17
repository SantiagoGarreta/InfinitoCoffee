namespace InfinitoCoffee.Application.Users.Commands;

public sealed record DeactivateUserCommand(Guid UserId, Guid ActingUserId);
