using InfinitoCoffee.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfinitoCoffee.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(user => user.Id);

        builder.Property(user => user.Username)
            .IsRequired()
            .HasMaxLength(User.UsernameMaxLength);

        builder.Property(user => user.NormalizedUsername)
            .IsRequired()
            .HasMaxLength(User.UsernameMaxLength);

        builder.HasIndex(user => user.NormalizedUsername)
            .HasDatabaseName("IX_Users_NormalizedUsername")
            .IsUnique();

        builder.Property(user => user.DisplayName)
            .IsRequired()
            .HasMaxLength(User.DisplayNameMaxLength);

        builder.Property(user => user.PasswordHash)
            .IsRequired()
            .HasMaxLength(User.PasswordHashMaxLength);

        builder.Property(user => user.Role)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(user => user.IsActive)
            .IsRequired();

        builder.Property(user => user.IsSystemUser)
            .IsRequired();

        builder.HasIndex(user => user.IsSystemUser)
            .HasDatabaseName("UX_Users_SingleSystemUser")
            .HasFilter("[IsSystemUser] = 1")
            .IsUnique();
    }
}
