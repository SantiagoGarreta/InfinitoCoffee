using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace InfinitoCoffee.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBranches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockMovements_ItemId_Location",
                table: "StockMovements");

            migrationBuilder.DropPrimaryKey(
                name: "PK_StockBalances",
                table: "StockBalances");

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "StockOperations",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "StockMovements",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "StockBalances",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "Orders",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddPrimaryKey(
                name: "PK_StockBalances",
                table: "StockBalances",
                columns: new[] { "ItemId", "Location", "BranchId" });

            migrationBuilder.CreateTable(
                name: "Branches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Branches", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Branches",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Sucursal 1" },
                    { 2, "Sucursal 2" }
                });

            migrationBuilder.Sql("""
                UPDATE b SET b.BranchId = 1
                FROM StockBalances b INNER JOIN StockItems i ON i.Id = b.ItemId
                WHERE i.Kind = 2;
                UPDATE m SET m.BranchId = 1
                FROM StockMovements m INNER JOIN StockItems i ON i.Id = m.ItemId
                WHERE i.Kind = 2;
                INSERT INTO StockBalances (ItemId, Location, BranchId, Quantity, Revision)
                SELECT Id, 1, 2, 0, NEWID() FROM StockItems WHERE Kind = 2;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Users_BranchId_Id",
                table: "Users",
                columns: new[] { "BranchId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_StockOperations_BranchId",
                table: "StockOperations",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ItemId_Location_BranchId",
                table: "StockMovements",
                columns: new[] { "ItemId", "Location", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_BranchId_Id",
                table: "Orders",
                columns: new[] { "BranchId", "Id" });

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Branches_BranchId",
                table: "Orders",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockOperations_Branches_BranchId",
                table: "StockOperations",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Branches_BranchId",
                table: "Users",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Branches_BranchId",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_StockOperations_Branches_BranchId",
                table: "StockOperations");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Branches_BranchId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "Branches");

            migrationBuilder.DropIndex(
                name: "IX_Users_BranchId_Id",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_StockOperations_BranchId",
                table: "StockOperations");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_ItemId_Location_BranchId",
                table: "StockMovements");

            migrationBuilder.DropPrimaryKey(
                name: "PK_StockBalances",
                table: "StockBalances");

            migrationBuilder.DropIndex(
                name: "IX_Orders_BranchId_Id",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "StockOperations");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "StockMovements");

            // Returning to one stock balance preserves the total of both branches.
            migrationBuilder.Sql("""
                UPDATE b SET b.Quantity = totals.Quantity
                FROM StockBalances b INNER JOIN (
                    SELECT ItemId, Location, SUM(Quantity) AS Quantity
                    FROM StockBalances GROUP BY ItemId, Location
                ) totals ON totals.ItemId = b.ItemId AND totals.Location = b.Location;
                DELETE b FROM StockBalances b
                WHERE b.BranchId > (SELECT MIN(other.BranchId) FROM StockBalances other
                    WHERE other.ItemId = b.ItemId AND other.Location = b.Location);
                """);

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "StockBalances");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "Orders");

            migrationBuilder.AddPrimaryKey(
                name: "PK_StockBalances",
                table: "StockBalances",
                columns: new[] { "ItemId", "Location" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ItemId_Location",
                table: "StockMovements",
                columns: new[] { "ItemId", "Location" });
        }
    }
}
