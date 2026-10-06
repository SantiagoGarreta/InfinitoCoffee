namespace InfinitoCoffee.Domain.Branches;

public sealed class Branch
{
    public const int DefaultId = 1;
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

}
