namespace InfinitoCoffee.Api.Authorization;

public static class AuthorizationPolicyNames
{
    public const string AdministratorOnly = nameof(AdministratorOnly);
    public const string AdministratorOrCashier = nameof(AdministratorOrCashier);
    public const string AdministratorOrKitchen = nameof(AdministratorOrKitchen);
    public const string AllOperationalRoles = nameof(AllOperationalRoles);
}
