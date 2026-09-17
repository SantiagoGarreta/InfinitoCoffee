using System.ComponentModel.DataAnnotations;
using InfinitoCoffee.Domain.Users;

namespace InfinitoCoffee.Api.Contracts.Authentication;

public sealed class LoginRequest
{
    [Required]
    [MinLength(User.UsernameMinLength)]
    [MaxLength(User.UsernameMaxLength)]
    public string Username { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}
