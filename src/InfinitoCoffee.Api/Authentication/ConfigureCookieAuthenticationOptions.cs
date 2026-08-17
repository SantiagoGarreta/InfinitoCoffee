using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace InfinitoCoffee.Api.Authentication;

public sealed class ConfigureCookieAuthenticationOptions : IConfigureNamedOptions<CookieAuthenticationOptions>
{
    private readonly IHostEnvironment _environment;

    public ConfigureCookieAuthenticationOptions(IHostEnvironment environment)
    {
        _environment = environment;
    }

    public void Configure(CookieAuthenticationOptions options)
    {
        Configure(AuthenticationConstants.CookieScheme, options);
    }

    public void Configure(string? name, CookieAuthenticationOptions options)
    {
        if (name != AuthenticationConstants.CookieScheme)
        {
            return;
        }

        options.Cookie.Name = AuthenticationConstants.CookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.Path = "/";
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = _environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = AuthenticationConstants.SessionDuration;
        options.SlidingExpiration = false;
        options.EventsType = typeof(ApiCookieAuthenticationEvents);
    }
}
