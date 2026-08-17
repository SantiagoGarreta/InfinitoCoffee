using System.ComponentModel.DataAnnotations;
using InfinitoCoffee.Application.Users.Services;

namespace InfinitoCoffee.Api.Contracts.Users;

public sealed class ResetUserPasswordRequest
{
    [Required]
    [MaxLength(UserAdministrationService.PasswordMaxLength)]
    public string NewPassword { get; init; } = string.Empty;
}
