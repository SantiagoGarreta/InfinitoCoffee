namespace InfinitoCoffee.Infrastructure.Persistence.Seed;

public sealed class InitialSystemUserOptions
{
    public const string SectionName = "InitialSystemUser";

    public string? Username { get; init; }

    public string? DisplayName { get; init; }

    public string? Password { get; init; }
}
