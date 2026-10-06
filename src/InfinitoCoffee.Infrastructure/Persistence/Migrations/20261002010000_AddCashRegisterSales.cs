using InfinitoCoffee.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace InfinitoCoffee.Infrastructure.Persistence.Migrations;

[DbContext(typeof(InfinitoCoffeeDbContext))]
[Migration("20261002010000_AddCashRegisterSales")]
public sealed class AddCashRegisterSales : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_StockOperations_OrderId", table: "StockOperations");
        migrationBuilder.CreateIndex(name: "IX_StockOperations_OrderId", table: "StockOperations", column: "OrderId");

        migrationBuilder.AddColumn<DateTime>(
            name: "SoldAtUtc",
            table: "OrderItems",
            type: ActiveProvider?.Contains("Sqlite") == true ? "TEXT" : "datetime2",
            nullable: true);

        if (ActiveProvider?.Contains("Sqlite") == true)
        {
            migrationBuilder.Sql("INSERT INTO ProductCategories (Id, Name, IsActive) SELECT 'a913122d-703e-4f01-af17-1195849f75ed', 'Cantina', 1 WHERE NOT EXISTS (SELECT 1 FROM ProductCategories WHERE lower(Name) = 'cantina');");
        }
        else
        {
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM ProductCategories WHERE LOWER(Name) = 'cantina') INSERT INTO ProductCategories (Id, Name, IsActive) VALUES (NEWID(), 'Cantina', 1);");
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "SoldAtUtc", table: "OrderItems");
        migrationBuilder.DropIndex(name: "IX_StockOperations_OrderId", table: "StockOperations");
        migrationBuilder.CreateIndex(
            name: "IX_StockOperations_OrderId",
            table: "StockOperations",
            column: "OrderId",
            unique: true,
            filter: "[OrderId] IS NOT NULL");
    }
}
