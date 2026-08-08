using InfinitoCoffee.Domain.Users;
using InfinitoCoffee.Infrastructure;
using InfinitoCoffee.Infrastructure.Persistence;
using InfinitoCoffee.Infrastructure.Persistence.Maintenance;
using InfinitoCoffee.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var command = args.FirstOrDefault()?.Trim().ToLowerInvariant();

if (command is not "migrate" and not "seed" and not "reset-system-password")
{
    Console.Error.WriteLine(
        "Usage: dotnet run --project .\\src\\InfinitoCoffee.DbSetup -- [migrate|seed|reset-system-password]");
    return 1;
}

var builder = Host.CreateApplicationBuilder();
ConfigureApplication(builder);

builder.Services.AddInfrastructure(builder.Configuration);

if (command is "seed" or "reset-system-password")
{
    builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
}

if (command == "seed")
{
    builder.Services.Configure<InitialSystemUserOptions>(
        builder.Configuration.GetSection(InitialSystemUserOptions.SectionName));
    builder.Services.AddScoped<DevelopmentDataSeeder>();
}

if (command == "reset-system-password")
{
    builder.Services.Configure<SystemUserPasswordResetOptions>(options =>
    {
        options.NewPassword =
            builder.Configuration[SystemUserPasswordResetOptions.NewPasswordConfigurationKey];
    });
    builder.Services.AddScoped<SystemUserPasswordResetter>();
}

using var host = builder.Build();
using var scope = host.Services.CreateScope();

var services = scope.ServiceProvider;

switch (command)
{
    case "migrate":
        Console.WriteLine("Applying EF Core migrations...");
        var migrationDbContext = services.GetRequiredService<InfinitoCoffeeDbContext>();
        await migrationDbContext.Database.MigrateAsync();
        Console.WriteLine("Migrations applied successfully.");
        return 0;

    case "seed":
        if (!builder.Environment.IsDevelopment())
        {
            Console.Error.WriteLine("The development seed can only run when DOTNET_ENVIRONMENT=Development.");
            return 2;
        }

        Console.WriteLine("Applying development seed data...");
        var seedDbContext = services.GetRequiredService<InfinitoCoffeeDbContext>();
        var seeder = services.GetRequiredService<DevelopmentDataSeeder>();
        await seeder.SeedAsync(seedDbContext);
        Console.WriteLine("Development seed completed successfully.");
        return 0;

    case "reset-system-password":
        Console.WriteLine("Resetting system user password...");
        var passwordResetter = services.GetRequiredService<SystemUserPasswordResetter>();
        await passwordResetter.ResetAsync();
        Console.WriteLine("System user password reset successfully.");
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
