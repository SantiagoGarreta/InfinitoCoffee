namespace InfinitoCoffee.Api.Authentication;

public static class AuthenticationConstants
{
    public const string CookieScheme = "Cookies";
    public const string CookieName = "InfinitoCoffee.Auth";
    public const string DisplayNameClaimType = "display_name";

    public static readonly TimeSpan SessionDuration = TimeSpan.FromHours(8);
}
