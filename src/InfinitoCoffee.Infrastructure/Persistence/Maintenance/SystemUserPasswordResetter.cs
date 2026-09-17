using InfinitoCoffee.Domain.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InfinitoCoffee.Infrastructure.Persistence.Maintenance;

public sealed class SystemUserPasswordResetter
{
    private readonly InfinitoCoffeeDbContext _dbContext;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IOptions<SystemUserPasswordResetOptions> _options;

    public SystemUserPasswordResetter(
        InfinitoCoffeeDbContext dbContext,
        IPasswordHasher<User> passwordHasher,
        IOptions<SystemUserPasswordResetOptions> options)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _options = options;
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        var systemUsers = await _dbContext.Users
            .Where(user => user.IsSystemUser)
            .Take(2)
            .ToListAsync(cancellationToken);

        if (systemUsers.Count == 0)
        {
            throw new InvalidOperationException("System user was not found.");
        }

        if (systemUsers.Count > 1)
        {
            throw new InvalidOperationException("More than one system user was found.");
        }

        var systemUser = systemUsers[0];

        if (!systemUser.IsActive)
        {
            throw new InvalidOperationException("The system user must be active.");
        }

        if (systemUser.Role != UserRole.Administrator)
        {
            throw new InvalidOperationException(
                "The system user must have the Administrator role.");
        }

        var newPassword = _options.Value.NewPassword;
        if (string.IsNullOrWhiteSpace(newPassword))
        {
            throw new InvalidOperationException(
                $"System user reset configuration '{SystemUserPasswordResetOptions.NewPasswordConfigurationKey}' is required.");
        }

        var newPasswordHash = _passwordHasher.HashPassword(systemUser, newPassword);
        systemUser.ChangePasswordHash(newPasswordHash);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
