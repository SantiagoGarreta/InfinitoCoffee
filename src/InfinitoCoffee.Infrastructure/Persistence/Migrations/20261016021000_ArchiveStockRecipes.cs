using InfinitoCoffee.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace InfinitoCoffee.Infrastructure.Persistence.Migrations;

[DbContext(typeof(InfinitoCoffeeDbContext))]
[Migration("20261016021000_ArchiveStockRecipes")]
public sealed class ArchiveStockRecipes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsArchived",
            table: "StockRecipes",
            type: "bit",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "IsArchived", table: "StockRecipes");
    }
}
