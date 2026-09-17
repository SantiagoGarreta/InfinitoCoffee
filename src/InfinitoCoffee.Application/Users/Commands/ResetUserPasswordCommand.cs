namespace InfinitoCoffee.Application.Users.Commands;

public sealed record ResetUserPasswordCommand(Guid UserId, string NewPassword);
