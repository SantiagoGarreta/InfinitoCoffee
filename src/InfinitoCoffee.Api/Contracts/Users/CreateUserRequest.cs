using System.ComponentModel.DataAnnotations;
using InfinitoCoffee.Application.Users.Services;
using InfinitoCoffee.Domain.Users;

namespace InfinitoCoffee.Api.Contracts.Users;

public sealed class CreateUserRequest
{
    [Required]
    [StringLength(User.UsernameMaxLength, MinimumLength = User.UsernameMinLength)]
    public string Username { get; init; } = string.Empty;

    [Required]
    [MaxLength(User.DisplayNameMaxLength)]
    public string DisplayName { get; init; } = string.Empty;

    [Required]
    [MaxLength(UserAdministrationService.PasswordMaxLength)]
    public string Password { get; init; } = string.Empty;

    [Required]
    [EnumDataType(typeof(UserRole))]
    public UserRole? Role { get; init; }
}
