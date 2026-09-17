using InfinitoCoffee.Api.Configuration;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;

namespace InfinitoCoffee.Api.Extensions;

public sealed class ConfigureCorsOptions : IConfigureOptions<CorsOptions>
{
    private readonly IOptions<CorsSettings> _corsSettings;

    public ConfigureCorsOptions(IOptions<CorsSettings> corsSettings)
    {
        _corsSettings = corsSettings;
    }

    public void Configure(CorsOptions options)
    {
        options.AddPolicy(ApiServiceCollectionExtensions.DevelopmentCorsPolicyName, policy =>
        {
            var origins = _corsSettings.Value.AllowedOrigins;

            if (origins.Length > 0)
            {
                policy.WithOrigins(origins);
            }

            policy
                .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
                .WithHeaders(
                    "Content-Type",
                    "Authorization",
                    "X-SignalR-User-Agent",
                    Antiforgery.AntiforgeryConstants.HeaderName)
                .AllowCredentials();
        });
    }
}
