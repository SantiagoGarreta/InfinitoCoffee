using InfinitoCoffee.Infrastructure;
using InfinitoCoffee.Infrastructure.Persistence;
using InfinitoCoffee.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var command = args.FirstOrDefault()?.Trim().ToLowerInvariant();

if (command is not "migrate" and not "seed")
{
    Console.Error.WriteLine("Usage: dotnet run --project .\\src\\InfinitoCoffee.DbSetup -- [migrate|seed]");
    return 1;
}

var builder = Host.CreateApplicationBuilder();
ConfigureApplication(builder);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<DevelopmentDataSeeder>();

using var host = builder.Build();
using var scope = host.Services.CreateScope();

var services = scope.ServiceProvider;
var dbContext = services.GetRequiredService<InfinitoCoffeeDbContext>();

switch (command)
{
    case "migrate":
        Console.WriteLine("Applying EF Core migrations...");
        await dbContext.Database.MigrateAsync();
        Console.WriteLine("Migrations applied successfully.");
        return 0;

    case "seed":
        if (!builder.Environment.IsDevelopment())
        {
            Console.Error.WriteLine("The development seed can only run when DOTNET_ENVIRONMENT=Development.");
            return 2;
        }

        Console.WriteLine("Applying development seed data...");
        var seeder = services.GetRequiredService<DevelopmentDataSeeder>();
        await seeder.SeedAsync(dbContext);
        Console.WriteLine("Development seed completed successfully.");
        return 0;

    default:
        return 1;
}

static void ConfigureApplication(HostApplicationBuilder builder)
{
    var currentDirectory = Directory.GetCurrentDirectory();
    var apiProjectDirectory = Path.Combine(currentDirectory, "src", "InfinitoCoffee.Api");

    builder.Configuration.AddJsonFile(
        Path.Combine(apiProjectDirectory, "appsettings.json"),
        optional: true,
        reloadOnChange: false);

    builder.Configuration.AddJsonFile(
        Path.Combine(apiProjectDirectory, $"appsettings.{builder.Environment.EnvironmentName}.json"),
        optional: true,
        reloadOnChange: false);
}
