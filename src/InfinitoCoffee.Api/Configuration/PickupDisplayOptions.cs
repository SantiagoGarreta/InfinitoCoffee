namespace InfinitoCoffee.Api.Configuration;

public sealed class PickupDisplayOptions
{
    public const string SectionName = "PickupDisplay";

    public int ReadyVisibilityMinutes { get; init; } = 15;
}
