using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Extensions.Options;

namespace InfinitoCoffee.Api.Antiforgery;

public sealed class ConfigureAntiforgeryOptions : IConfigureOptions<AntiforgeryOptions>
{
    private readonly IHostEnvironment _environment;

    public ConfigureAntiforgeryOptions(IHostEnvironment environment)
    {
        _environment = environment;
    }

    public void Configure(AntiforgeryOptions options)
    {
        options.HeaderName = AntiforgeryConstants.HeaderName;
        options.Cookie.Name = AntiforgeryConstants.CookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.Path = "/";
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = _environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
    }
}
