namespace InfinitoCoffee.Infrastructure.Persistence.Maintenance;

public sealed class SystemUserPasswordResetOptions
{
    public const string NewPasswordConfigurationKey = "SYSTEM_USER_NEW_PASSWORD";

    public string? NewPassword { get; set; }
}
