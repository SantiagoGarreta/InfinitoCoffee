using System.Text.RegularExpressions;

namespace InfinitoCoffee.Domain.Users;

public class User
{
    public const int UsernameMinLength = 3;
    public const int UsernameMaxLength = 50;
    public const int DisplayNameMaxLength = 100;
    public const int PasswordHashMaxLength = 512;

    private static readonly Regex UsernamePattern = new(
        "^[A-Za-z0-9](?:[A-Za-z0-9._-]*[A-Za-z0-9])?$",
        RegexOptions.CultureInvariant);

    private User()
    {
        Username = string.Empty;
        NormalizedUsername = string.Empty;
        DisplayName = string.Empty;
        PasswordHash = string.Empty;
    }

    public User(
        string username,
        string displayName,
        string passwordHash,
        UserRole role)
    {
        var validatedUsername = ValidateUsername(username);
        var validatedDisplayName = ValidateDisplayName(displayName);
        ValidatePasswordHash(passwordHash);
        ValidateRole(role);

        Id = Guid.NewGuid();
        Username = validatedUsername;
        NormalizedUsername = NormalizeUsername(validatedUsername);
        DisplayName = validatedDisplayName;
        PasswordHash = passwordHash;
        Role = role;
        IsActive = true;
    }

    public Guid Id { get; private set; }

    public string Username { get; private set; }

    public string NormalizedUsername { get; private set; }

    public string DisplayName { get; private set; }

    public string PasswordHash { get; private set; }

    public UserRole Role { get; private set; }

    public bool IsActive { get; private set; }

    public void ChangeUsername(string username)
    {
        var validatedUsername = ValidateUsername(username);

        Username = validatedUsername;
        NormalizedUsername = NormalizeUsername(validatedUsername);
    }

    public void ChangeDisplayName(string displayName)
    {
        DisplayName = ValidateDisplayName(displayName);
    }

    public void ChangePasswordHash(string passwordHash)
    {
        ValidatePasswordHash(passwordHash);
        PasswordHash = passwordHash;
    }

    public void ChangeRole(UserRole role)
    {
        ValidateRole(role);
        Role = role;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    private static string ValidateUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Username is required.", nameof(username));
        }

        var trimmedUsername = username.Trim();

        if (trimmedUsername.Length < UsernameMinLength || trimmedUsername.Length > UsernameMaxLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(username),
                $"Username must be between {UsernameMinLength} and {UsernameMaxLength} characters long.");
        }

        if (!UsernamePattern.IsMatch(trimmedUsername))
        {
            throw new ArgumentException(
                "Username must start and end with a letter or number and may contain only ASCII letters, numbers, dots, hyphens, and underscores.",
                nameof(username));
        }

        return trimmedUsername;
    }

    private static string NormalizeUsername(string username)
    {
        return username.ToUpperInvariant();
    }

    private static string ValidateDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name is required.", nameof(displayName));
        }

        var trimmedDisplayName = displayName.Trim();

        if (trimmedDisplayName.Length > DisplayNameMaxLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(displayName),
                $"Display name cannot exceed {DisplayNameMaxLength} characters.");
        }

        return trimmedDisplayName;
    }

    private static void ValidatePasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException("Password hash is required.", nameof(passwordHash));
        }

        if (passwordHash.Length > PasswordHashMaxLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(passwordHash),
                $"Password hash cannot exceed {PasswordHashMaxLength} characters.");
        }
    }

    private static void ValidateRole(UserRole role)
    {
        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role), role, "Role is not valid.");
        }
    }
}
