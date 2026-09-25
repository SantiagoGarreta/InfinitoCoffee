namespace InfinitoCoffee.Domain.Stock;

public enum StockLocation { Factory = 1, Cafe = 2, Transit = 3 }
public enum StockItemKind { Ingredient = 1, FinishedProduct = 2 }
public enum StockUnit { Unit = 1, Gram = 2, Milliliter = 3 }

public sealed class StockItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public StockItemKind Kind { get; set; }
    public StockUnit Unit { get; set; }
    public Guid? ProductId { get; set; }
    public decimal MinimumQuantity { get; set; }
}

public sealed class StockBalance
{
    public Guid ItemId { get; set; }
    public StockLocation Location { get; set; }
    public decimal Quantity { get; set; }
    // Application-managed token works on both SQL Server and the SQLite test provider.
    public Guid Revision { get; set; } = Guid.NewGuid();
}

public sealed class StockRecipe
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ItemId { get; set; }
    public int Version { get; set; }
    public decimal Yield { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public Guid CreatedBy { get; set; }
    public List<StockRecipeLine> Lines { get; set; } = [];
}

public sealed class StockRecipeLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RecipeId { get; set; }
    public Guid ItemId { get; set; }
    public decimal Quantity { get; set; }
}

public sealed class StockOperation
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public Guid? ActorId { get; set; }
    public string Notes { get; set; } = string.Empty;
    public Guid? RecipeId { get; set; }
    public Guid? OrderId { get; set; }
    public List<StockMovement> Movements { get; set; } = [];
}

public sealed class StockMovement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OperationId { get; set; }
    public Guid ItemId { get; set; }
    public StockLocation Location { get; set; }
    public decimal Before { get; set; }
    public decimal Delta { get; set; }
    public decimal After { get; set; }
}

public sealed class StockTransfer
{
    public Guid Id { get; set; }
    public StockLocation From { get; set; }
    public StockLocation To { get; set; }
    public DateTime SentAtUtc { get; set; }
    public DateTime? ReceivedAtUtc { get; set; }
    public Guid? ReceiptOperationId { get; set; }
    public Guid Revision { get; set; } = Guid.NewGuid();
    public List<StockTransferLine> Lines { get; set; } = [];
}

public sealed class StockTransferLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TransferId { get; set; }
    public Guid ItemId { get; set; }
    public decimal Sent { get; set; }
    public decimal? Received { get; set; }
}

public static class StockQuantity
{
    public const decimal Maximum = 999999999m;

    public static decimal Convert(decimal quantity, string? inputUnit, StockUnit baseUnit, bool allowZero = false)
    {
        var factor = (baseUnit, inputUnit) switch
        {
            (StockUnit.Unit, "unit") => 1m,
            (StockUnit.Gram, "g") => 1m,
            (StockUnit.Gram, "kg") => 1000m,
            (StockUnit.Milliliter, "ml") => 1m,
            (StockUnit.Milliliter, "l") => 1000m,
            _ => throw new ArgumentException("La unidad no corresponde al artículo.")
        };
        if (quantity < 0 || (!allowZero && quantity == 0) || quantity > Maximum)
            throw new ArgumentException("Ingresá una cantidad válida mayor que cero.");
        var value = quantity * factor;
        ValidateBase(value, baseUnit);
        return value;
    }

    public static void ValidateBase(decimal value, StockUnit unit)
    {
        if (value < 0 || value > Maximum || decimal.Round(value, 3) != value
            || (unit == StockUnit.Unit && decimal.Truncate(value) != value))
            throw new ArgumentException("La cantidad debe respetar la unidad (unidades enteras; gramos y ml con hasta 3 decimales). Usá un lote completo si la receta necesita fracciones de una unidad.");
    }
}
