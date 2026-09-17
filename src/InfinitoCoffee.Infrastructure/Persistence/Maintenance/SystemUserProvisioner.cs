using InfinitoCoffee.Domain.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using InfinitoCoffee.Infrastructure.Persistence.Seed;

namespace InfinitoCoffee.Infrastructure.Persistence.Maintenance;

public sealed class SystemUserProvisioner
{
    private readonly InfinitoCoffeeDbContext _dbContext;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IOptions<InitialSystemUserOptions> _options;

    public SystemUserProvisioner(
        InfinitoCoffeeDbContext dbContext,
        IPasswordHasher<User> passwordHasher,
        IOptions<InitialSystemUserOptions> options)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _options = options;
    }

    public async Task ProvisionAsync(CancellationToken cancellationToken = default)
    {
        var existingSystemUser = await _dbContext.Users
            .SingleOrDefaultAsync(user => user.IsSystemUser, cancellationToken);

        if (existingSystemUser is not null)
        {
            if (!existingSystemUser.IsActive || existingSystemUser.Role != UserRole.Administrator)
            {
                throw new InvalidOperationException(
                    "The existing system user must be active and have the Administrator role.");
            }

            return;
        }

        var options = _options.Value;
        var username = GetRequiredValue(options.Username, "INITIAL_ADMIN_USERNAME");
        var displayName = GetRequiredValue(options.DisplayName, "INITIAL_ADMIN_DISPLAY_NAME");
        var password = GetRequiredValue(options.Password, "INITIAL_ADMIN_PASSWORD");

        var systemUser = User.CreateSystemUser(
            username,
            displayName,
            user => _passwordHasher.HashPassword(user, password));

        _dbContext.Users.Add(systemUser);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string GetRequiredValue(string? value, string environmentVariableName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Required system user configuration '{environmentVariableName}' is missing.");
        }

        return value;
    }
}
