using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfinitoCoffee.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ArchiveCatalogEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockItems_NormalizedName",
                table: "StockItems");

            migrationBuilder.DropIndex(
                name: "IX_StockItems_ProductId",
                table: "StockItems");

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "StockItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Products",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_StockItems_NormalizedName",
                table: "StockItems",
                column: "NormalizedName",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_StockItems_ProductId",
                table: "StockItems",
                column: "ProductId",
                unique: true,
                filter: "[ProductId] IS NOT NULL AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockItems_NormalizedName",
                table: "StockItems");

            migrationBuilder.DropIndex(
                name: "IX_StockItems_ProductId",
                table: "StockItems");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "StockItems");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Products");

            migrationBuilder.CreateIndex(
                name: "IX_StockItems_NormalizedName",
                table: "StockItems",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockItems_ProductId",
                table: "StockItems",
                column: "ProductId",
                unique: true,
                filter: "[ProductId] IS NOT NULL");
        }
    }
}
