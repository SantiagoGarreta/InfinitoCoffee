using InfinitoCoffee.Application.Common.Time;
using InfinitoCoffee.Application.Orders.Contracts;
using InfinitoCoffee.Application.ProductCategories.Contracts;
using InfinitoCoffee.Application.Products.Contracts;
using InfinitoCoffee.Infrastructure.Persistence;
using InfinitoCoffee.Infrastructure.Persistence.Repositories;
using InfinitoCoffee.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InfinitoCoffee.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("InfinitoCoffee");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'InfinitoCoffee' was not found.");
        }

        services.AddDbContext<InfinitoCoffeeDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IProductCategoryRepository, ProductCategoryRepository>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        return services;
    }
}
