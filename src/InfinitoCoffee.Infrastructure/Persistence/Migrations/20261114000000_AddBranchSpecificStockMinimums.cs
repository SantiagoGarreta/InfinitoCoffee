using InfinitoCoffee.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace InfinitoCoffee.Infrastructure.Persistence.Migrations;

[DbContext(typeof(InfinitoCoffeeDbContext))]
[Migration("20261114000000_AddBranchSpecificStockMinimums")]
public sealed class AddBranchSpecificStockMinimums : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "MinimumQuantityBranch2",
            table: "StockItems",
            type: "decimal(18,3)",
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.Sql("UPDATE StockItems SET MinimumQuantityBranch2 = MinimumQuantity");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "MinimumQuantityBranch2", table: "StockItems");
    }
}
