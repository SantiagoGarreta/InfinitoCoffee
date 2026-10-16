using InfinitoCoffee.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace InfinitoCoffee.Infrastructure.Persistence.Migrations;

[DbContext(typeof(InfinitoCoffeeDbContext))]
[Migration("20261016020000_ClassifyCoffeesAsMadeToOrder")]
public sealed class ClassifyCoffeesAsMadeToOrder : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DECLARE @Converted TABLE (Id uniqueidentifier NOT NULL);
            UPDATE s SET Kind = 3, MinimumQuantity = 0
            OUTPUT inserted.Id INTO @Converted (Id)
            FROM StockItems s
            JOIN Products p ON p.Id = s.ProductId
            JOIN ProductCategories c ON c.Id = p.CategoryId
            WHERE p.IsDeleted = 0 AND s.IsDeleted = 0 AND s.Kind = 2
              AND c.Name IN (N'Cafes', N'Cafés')
              AND NOT EXISTS (SELECT 1 FROM StockBalances b WHERE b.ItemId = s.Id AND b.Quantity <> 0)
              AND NOT EXISTS (SELECT 1 FROM StockMovements m WHERE m.ItemId = s.Id);

            DELETE b FROM StockBalances b
            JOIN @Converted converted ON converted.Id = b.ItemId;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Classification may be followed by sales and recipes; do not rewrite historical data.
    }
}
