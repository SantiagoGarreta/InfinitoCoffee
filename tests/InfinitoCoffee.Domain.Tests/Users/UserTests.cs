using InfinitoCoffee.Domain.Users;

namespace InfinitoCoffee.Domain.Tests.Users;

public class UserTests
{
    [Fact]
    public void NormalizeUsername_WithValidValue_UsesDomainValidationAndInvariantNormalization()
    {
        var normalizedUsername = User.NormalizeUsername("  Cashier.One-2  ");

        Assert.Equal("CASHIER.ONE-2", normalizedUsername);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid username")]
    [InlineData("administraciÃ³n")]
    public void NormalizeUsername_WithInvalidValue_ThrowsArgumentException(string? username)
    {
        Assert.ThrowsAny<ArgumentException>(() => User.NormalizeUsername(username!));
    }

    [Fact]
    public void Constructor_WithValidValues_CreatesActiveUser()
    {
        const string passwordHash = "  hash-preserved-exactly  ";

        var user = new User(
            "  cashier_2  ",
            "  María — Caja  ",
            passwordHash,
            UserRole.Cashier);

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal("cashier_2", user.Username);
        Assert.Equal("CASHIER_2", user.NormalizedUsername);
        Assert.Equal("María — Caja", user.DisplayName);
        Assert.Equal(passwordHash, user.PasswordHash);
        Assert.Equal(UserRole.Cashier, user.Role);
        Assert.True(user.IsActive);
        Assert.False(user.IsSystemUser);
    }

    [Fact]
    public void CreateSystemUser_WithValidValues_CreatesActiveAdministratorAndHashesAtomically()
    {
        User? userSeenByHashFactory = null;

        var user = User.CreateSystemUser(
            "  root.admin  ",
            "  System Administrator  ",
            candidate =>
            {
                userSeenByHashFactory = candidate;
                Assert.NotEqual(Guid.Empty, candidate.Id);
                Assert.Equal("root.admin", candidate.Username);
                Assert.Equal("ROOT.ADMIN", candidate.NormalizedUsername);
                Assert.Equal(UserRole.Administrator, candidate.Role);
                Assert.True(candidate.IsActive);
                Assert.True(candidate.IsSystemUser);
                return "system-password-hash";
            });

        Assert.Same(user, userSeenByHashFactory);
        Assert.Equal("System Administrator", user.DisplayName);
        Assert.Equal("system-password-hash", user.PasswordHash);
    }

    [Fact]
    public void CreateSystemUser_WithNullHashFactory_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            User.CreateSystemUser("root", "Root", null!));

        Assert.Equal("passwordHashFactory", exception.ParamName);
    }

    [Fact]
    public void CreateSystemUser_WithInvalidHash_ThrowsWithoutReturningUser()
    {
        Assert.Throws<ArgumentException>(() =>
            User.CreateSystemUser("root", "Root", _ => "   "));
    }

    [Fact]
    public void SystemUser_ChangeUsername_IsRejectedAndPreservesUsername()
    {
        var user = CreateSystemUser();

        Assert.Throws<InvalidOperationException>(() => user.ChangeUsername("another-root"));

        Assert.Equal("root", user.Username);
        Assert.Equal("ROOT", user.NormalizedUsername);
    }

    [Fact]
    public void SystemUser_ChangeDisplayName_IsRejectedAndPreservesDisplayName()
    {
        var user = CreateSystemUser();

        Assert.Throws<InvalidOperationException>(() => user.ChangeDisplayName("Another Root"));

        Assert.Equal("System Administrator", user.DisplayName);
    }

    [Fact]
    public void SystemUser_ChangeRole_IsRejectedAndPreservesAdministratorRole()
    {
        var user = CreateSystemUser();

        Assert.Throws<InvalidOperationException>(() => user.ChangeRole(UserRole.Cashier));

        Assert.Equal(UserRole.Administrator, user.Role);
    }

    [Fact]
    public void SystemUser_Deactivate_IsRejectedAndPreservesActiveState()
    {
        var user = CreateSystemUser();

        Assert.Throws<InvalidOperationException>(user.Deactivate);

        Assert.True(user.IsActive);
    }

    [Fact]
    public void SystemUser_ChangePasswordHash_IsAllowed()
    {
        var user = CreateSystemUser();

        user.ChangePasswordHash("new-system-hash");

        Assert.Equal("new-system-hash", user.PasswordHash);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("caja-1")]
    [InlineData("cocina.turno")]
    [InlineData("cashier_2")]
    [InlineData("A12")]
    public void Constructor_WithAllowedUsernameCharacters_AcceptsUsername(string username)
    {
        var user = CreateUser(username);

        Assert.Equal(username, user.Username);
        Assert.Equal(username.ToUpperInvariant(), user.NormalizedUsername);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithMissingUsername_ThrowsArgumentException(string? username)
    {
        var exception = Assert.Throws<ArgumentException>(() => CreateUser(username!));

        Assert.Equal("username", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithUsernameShorterThanMinimum_ThrowsArgumentOutOfRangeException()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => CreateUser("A1"));

        Assert.Equal("username", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithUsernameLongerThanMaximum_ThrowsArgumentOutOfRangeException()
    {
        var username = new string('a', User.UsernameMaxLength + 1);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => CreateUser(username));

        Assert.Equal("username", exception.ParamName);
    }

    [Theory]
    [InlineData("caja turno")]
    [InlineData("cajero@local")]
    [InlineData("caja/local")]
    [InlineData("caja:local")]
    [InlineData("administración")]
    [InlineData(".admin")]
    [InlineData("-admin")]
    [InlineData("_admin")]
    [InlineData("admin.")]
    [InlineData("admin-")]
    [InlineData("admin_")]
    public void Constructor_WithInvalidUsernameFormat_ThrowsArgumentException(string username)
    {
        var exception = Assert.Throws<ArgumentException>(() => CreateUser(username));

        Assert.Equal("username", exception.ParamName);
    }

    [Fact]
    public void ChangeUsername_WithValidValue_UpdatesUsernameAndNormalizedUsername()
    {
        var user = CreateUser();

        user.ChangeUsername("  Kitchen.Turno-2  ");

        Assert.Equal("Kitchen.Turno-2", user.Username);
        Assert.Equal("KITCHEN.TURNO-2", user.NormalizedUsername);
    }

    [Fact]
    public void ChangeUsername_WithInvalidValue_DoesNotModifyUsernames()
    {
        var user = CreateUser();

        Assert.Throws<ArgumentException>(() => user.ChangeUsername("invalid username"));

        Assert.Equal("admin", user.Username);
        Assert.Equal("ADMIN", user.NormalizedUsername);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithMissingDisplayName_ThrowsArgumentException(string? displayName)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new User("admin", displayName!, "hash", UserRole.Administrator));

        Assert.Equal("displayName", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithDisplayNameLongerThanMaximum_ThrowsArgumentOutOfRangeException()
    {
        var displayName = new string('a', User.DisplayNameMaxLength + 1);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new User("admin", displayName, "hash", UserRole.Administrator));

        Assert.Equal("displayName", exception.ParamName);
    }

    [Fact]
    public void ChangeDisplayName_WithValidValue_UpdatesTrimmedDisplayName()
    {
        var user = CreateUser();

        user.ChangeDisplayName("  María  del  Pilar  ");

        Assert.Equal("María  del  Pilar", user.DisplayName);
    }

    [Fact]
    public void ChangeDisplayName_WithInvalidValue_DoesNotModifyDisplayName()
    {
        var user = CreateUser();

        Assert.Throws<ArgumentException>(() => user.ChangeDisplayName("   "));

        Assert.Equal("Administrator", user.DisplayName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithMissingPasswordHash_ThrowsArgumentException(string? passwordHash)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new User("admin", "Administrator", passwordHash!, UserRole.Administrator));

        Assert.Equal("passwordHash", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithPasswordHashLongerThanMaximum_ThrowsArgumentOutOfRangeException()
    {
        var passwordHash = new string('h', User.PasswordHashMaxLength + 1);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new User("admin", "Administrator", passwordHash, UserRole.Administrator));

        Assert.Equal("passwordHash", exception.ParamName);
    }

    [Fact]
    public void ChangePasswordHash_WithValidValue_UpdatesHashWithoutTransformingIt()
    {
        var user = CreateUser();
        const string newHash = "  new-hash  ";

        user.ChangePasswordHash(newHash);

        Assert.Equal(newHash, user.PasswordHash);
    }

    [Fact]
    public void ChangePasswordHash_WithInvalidValue_DoesNotModifyHash()
    {
        var user = CreateUser();

        Assert.Throws<ArgumentException>(() => user.ChangePasswordHash("   "));

        Assert.Equal("hash", user.PasswordHash);
    }

    [Theory]
    [InlineData(UserRole.Administrator)]
    [InlineData(UserRole.Cashier)]
    [InlineData(UserRole.Kitchen)]
    public void Constructor_WithDefinedRole_AcceptsRole(UserRole role)
    {
        var user = new User("admin", "Administrator", "hash", role);

        Assert.Equal(role, user.Role);
    }

    [Fact]
    public void Constructor_WithUndefinedRole_ThrowsArgumentOutOfRangeException()
    {
        var invalidRole = (UserRole)999;

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new User("admin", "Administrator", "hash", invalidRole));

        Assert.Equal("role", exception.ParamName);
    }

    [Fact]
    public void ChangeRole_WithDefinedRole_UpdatesRole()
    {
        var user = CreateUser();

        user.ChangeRole(UserRole.Kitchen);

        Assert.Equal(UserRole.Kitchen, user.Role);
    }

    [Fact]
    public void ChangeRole_WithUndefinedRole_DoesNotModifyRole()
    {
        var user = CreateUser();

        Assert.Throws<ArgumentOutOfRangeException>(() => user.ChangeRole((UserRole)999));

        Assert.Equal(UserRole.Administrator, user.Role);
    }

    [Fact]
    public void ActivateAndDeactivate_AreIdempotentAndPreserveUserData()
    {
        var user = CreateUser();
        var originalId = user.Id;

        user.Deactivate();
        user.Deactivate();

        Assert.False(user.IsActive);
        AssertUserDataWasPreserved(user, originalId);

        user.Activate();
        user.Activate();

        Assert.True(user.IsActive);
        AssertUserDataWasPreserved(user, originalId);
    }

    private static User CreateUser(string username = "admin")
    {
        return new User(username, "Administrator", "hash", UserRole.Administrator);
    }

    private static User CreateSystemUser()
    {
        return User.CreateSystemUser(
            "root",
            "System Administrator",
            _ => "system-hash");
    }

    private static void AssertUserDataWasPreserved(User user, Guid expectedId)
    {
        Assert.Equal(expectedId, user.Id);
        Assert.Equal("admin", user.Username);
        Assert.Equal("ADMIN", user.NormalizedUsername);
        Assert.Equal("Administrator", user.DisplayName);
        Assert.Equal("hash", user.PasswordHash);
        Assert.Equal(UserRole.Administrator, user.Role);
    }
}
