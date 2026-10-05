namespace InfinitoCoffee.Domain.Branches;

public sealed class Branch
{
    public const int DefaultId = 1;
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
            throw new ArgumentException("Ingresá un nombre de sucursal de hasta 100 caracteres.");
        Name = name.Trim();
    }
}
