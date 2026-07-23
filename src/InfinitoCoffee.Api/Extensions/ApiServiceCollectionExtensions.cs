using System.Text.Json;
using System.Text.Json.Serialization;
using InfinitoCoffee.Api.Configuration;
using InfinitoCoffee.Api.ErrorHandling;
using InfinitoCoffee.Api.Health;
using InfinitoCoffee.Api.Realtime;
using InfinitoCoffee.Application.Orders.Contracts;
using InfinitoCoffee.Application.Orders.Services;
using InfinitoCoffee.Application.ProductCategories.Services;
using InfinitoCoffee.Application.Products.Services;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InfinitoCoffee.Api.Extensions;

public static class ApiServiceCollectionExtensions
{
    public const string DevelopmentCorsPolicyName = "DevelopmentCors";

    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<CorsSettings>(configuration.GetSection(CorsSettings.SectionName));
        services.Configure<PickupDisplayOptions>(configuration.GetSection(PickupDisplayOptions.SectionName));

        services
            .AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var problemDetails = new ValidationProblemDetails(context.ModelState)
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Validation Error",
                    Detail = "One or more validation errors occurred.",
                    Type = "https://httpstatuses.com/400"
                };
                problemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

                return new BadRequestObjectResult(problemDetails);
            };
        });

        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");
        services.AddSignalR();
        services.AddCors();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options => options.SupportNonNullableReferenceTypes());

        services.TryAddScoped<IOrderEventPublisher, NullOrderEventPublisher>();
        services.AddScoped<IOrderEventPublisher, SignalROrderEventPublisher>();
        services.AddScoped<OrderService>();
        services.AddScoped<ProductService>();
        services.AddScoped<ProductCategoryService>();

        services.AddSingleton<IConfigureOptions<CorsOptions>, ConfigureCorsOptions>();

        return services;
    }

    public static HealthCheckOptions CreateHealthCheckOptions()
    {
        return new HealthCheckOptions
        {
            ResultStatusCodes =
            {
                [Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy] = StatusCodes.Status200OK,
                [Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded] = StatusCodes.Status200OK,
                [Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
            },
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json";

                var payload = new
                {
                    status = report.Status.ToString(),
                    entries = report.Entries.ToDictionary(
                        entry => entry.Key,
                        entry => new
                        {
                            status = entry.Value.Status.ToString(),
                            description = entry.Value.Description
                        })
                };

                await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
            }
        };
    }
}
