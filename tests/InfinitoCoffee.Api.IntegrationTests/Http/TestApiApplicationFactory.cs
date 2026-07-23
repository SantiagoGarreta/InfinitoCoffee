using System.Data.Common;
using InfinitoCoffee.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InfinitoCoffee.Api.IntegrationTests.Http;

internal sealed class TestApiApplicationFactory : WebApplicationFactory<Program>, IAsyncDisposable
{
    private SqliteConnection? _connection;

    public async Task ExecuteDbContextAsync(Func<InfinitoCoffeeDbContext, Task> action)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InfinitoCoffeeDbContext>();
        await action(dbContext);
    }

    public async Task<T> ExecuteDbContextAsync<T>(Func<InfinitoCoffeeDbContext, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InfinitoCoffeeDbContext>();
        return await action(dbContext);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration(configurationBuilder =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:InfinitoCoffee"] = "Data Source=:memory:",
                ["Cors:AllowedOrigins:0"] = "http://localhost:4200",
                ["PickupDisplay:ReadyVisibilityMinutes"] = "15"
            });
        });

        builder.ConfigureServices(services =>
        {
            var sqlServerDescriptors = services
                .Where(descriptor =>
                    descriptor.ServiceType.FullName?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) == true
                    || descriptor.ImplementationType?.FullName?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) == true)
                .ToArray();

            foreach (var descriptor in sqlServerDescriptors)
            {
                services.Remove(descriptor);
            }

            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<DbContextOptions<InfinitoCoffeeDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<InfinitoCoffeeDbContext>>();
            services.RemoveAll<InfinitoCoffeeDbContext>();
            services.RemoveAll<DbConnection>();

            services.AddSingleton<DbConnection>(_ =>
            {
                _connection ??= new SqliteConnection("Data Source=:memory:");
                if (_connection.State != System.Data.ConnectionState.Open)
                {
                    _connection.Open();
                }

                return _connection;
            });

            services.AddDbContext<InfinitoCoffeeDbContext>((serviceProvider, options) =>
            {
                options.UseSqlite((SqliteConnection)serviceProvider.GetRequiredService<DbConnection>());
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        _connection?.Dispose();
        _connection = null;
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        Dispose();
        await ValueTask.CompletedTask;
    }

    public new HttpClient CreateClient()
    {
        var client = base.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InfinitoCoffeeDbContext>();
        dbContext.Database.EnsureCreated();

        return client;
    }
}
