using InfinitoCoffee.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace InfinitoCoffee.Infrastructure.Persistence.Migrations;

[DbContext(typeof(InfinitoCoffeeDbContext))]
[Migration("20261002000000_UnifyStockBalances")]
public sealed class UnifyStockBalances : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (ActiveProvider?.Contains("Sqlite") == true)
            migrationBuilder.Sql("UPDATE StockBalances SET Quantity = (SELECT SUM(other.Quantity) FROM StockBalances AS other WHERE other.ItemId = StockBalances.ItemId) WHERE Location = 1;");
        else
            migrationBuilder.Sql("UPDATE primaryBalance SET Quantity = totals.Quantity FROM StockBalances AS primaryBalance INNER JOIN (SELECT ItemId, SUM(Quantity) AS Quantity FROM StockBalances GROUP BY ItemId) AS totals ON totals.ItemId = primaryBalance.ItemId WHERE primaryBalance.Location = 1;");
        migrationBuilder.Sql("DELETE FROM StockBalances WHERE Location <> 1;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // The original distribution between locations cannot be reconstructed.
    }
}
