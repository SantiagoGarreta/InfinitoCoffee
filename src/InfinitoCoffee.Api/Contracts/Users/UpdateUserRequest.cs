using System.ComponentModel.DataAnnotations;
using InfinitoCoffee.Domain.Users;

namespace InfinitoCoffee.Api.Contracts.Users;

public sealed class UpdateUserRequest
{
    [Required]
    [StringLength(User.UsernameMaxLength, MinimumLength = User.UsernameMinLength)]
    public string Username { get; init; } = string.Empty;

    [Required]
    [MaxLength(User.DisplayNameMaxLength)]
    public string DisplayName { get; init; } = string.Empty;

    [Required]
    [EnumDataType(typeof(UserRole))]
    public UserRole? Role { get; init; }
}
