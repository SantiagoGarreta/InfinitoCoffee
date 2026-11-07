using InfinitoCoffee.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace InfinitoCoffee.Infrastructure.Persistence.Migrations;

[DbContext(typeof(InfinitoCoffeeDbContext))]
[Migration("20261107000000_AddTakeAwayServiceOption")]
public sealed class AddTakeAwayServiceOption : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>("CanConsumeOnSite", "Products", type: "bit", nullable: false, defaultValue: true);
        migrationBuilder.AddColumn<bool>("CanTakeAway", "Products", type: "bit", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<decimal>("TakeAwayCost", "Products", type: "decimal(18,2)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<bool>("IsTakeAway", "OrderItems", type: "bit", nullable: false, defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("IsTakeAway", "OrderItems");
        migrationBuilder.DropColumn("TakeAwayCost", "Products");
        migrationBuilder.DropColumn("CanTakeAway", "Products");
        migrationBuilder.DropColumn("CanConsumeOnSite", "Products");
    }
}
