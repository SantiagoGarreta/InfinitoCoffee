using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfinitoCoffee.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStockControl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StockItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Unit = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MinimumQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockBalances",
                columns: table => new
                {
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Location = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Revision = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockBalances", x => new { x.ItemId, x.Location });
                    table.CheckConstraint("CK_StockBalances_Quantity", "[Quantity] >= 0");
                    table.ForeignKey(
                        name: "FK_StockBalances_StockItems_ItemId",
                        column: x => x.ItemId,
                        principalTable: "StockItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockRecipes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Yield = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockRecipes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockRecipes_StockItems_ItemId",
                        column: x => x.ItemId,
                        principalTable: "StockItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockRecipes_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockOperations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RecipeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockOperations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockOperations_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockOperations_StockRecipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "StockRecipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockOperations_Users_ActorId",
                        column: x => x.ActorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockRecipeLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockRecipeLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockRecipeLines_StockItems_ItemId",
                        column: x => x.ItemId,
                        principalTable: "StockItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockRecipeLines_StockRecipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "StockRecipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StockMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Location = table.Column<int>(type: "int", nullable: false),
                    Before = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Delta = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    After = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockMovements_StockItems_ItemId",
                        column: x => x.ItemId,
                        principalTable: "StockItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockMovements_StockOperations_OperationId",
                        column: x => x.OperationId,
                        principalTable: "StockOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StockTransfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    From = table.Column<int>(type: "int", nullable: false),
                    To = table.Column<int>(type: "int", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReceiptOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Revision = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTransfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockTransfers_StockOperations_Id",
                        column: x => x.Id,
                        principalTable: "StockOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransfers_StockOperations_ReceiptOperationId",
                        column: x => x.ReceiptOperationId,
                        principalTable: "StockOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockTransferLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransferId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sent = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Received = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTransferLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockTransferLines_StockItems_ItemId",
                        column: x => x.ItemId,
                        principalTable: "StockItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockTransferLines_StockTransfers_TransferId",
                        column: x => x.TransferId,
                        principalTable: "StockTransfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ItemId_Location",
                table: "StockMovements",
                columns: new[] { "ItemId", "Location" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_OperationId",
                table: "StockMovements",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_StockOperations_ActorId",
                table: "StockOperations",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_StockOperations_CreatedAtUtc_Id",
                table: "StockOperations",
                columns: new[] { "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_StockOperations_OrderId",
                table: "StockOperations",
                column: "OrderId",
                unique: true,
                filter: "[OrderId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StockOperations_RecipeId",
                table: "StockOperations",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_StockRecipeLines_ItemId",
                table: "StockRecipeLines",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_StockRecipeLines_RecipeId_ItemId",
                table: "StockRecipeLines",
                columns: new[] { "RecipeId", "ItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockRecipes_CreatedBy",
                table: "StockRecipes",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_StockRecipes_ItemId_Version",
                table: "StockRecipes",
                columns: new[] { "ItemId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockTransferLines_ItemId",
                table: "StockTransferLines",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransferLines_TransferId_ItemId",
                table: "StockTransferLines",
                columns: new[] { "TransferId", "ItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_ReceiptOperationId",
                table: "StockTransfers",
                column: "ReceiptOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransfers_ReceivedAtUtc",
                table: "StockTransfers",
                column: "ReceivedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockBalances");

            migrationBuilder.DropTable(
                name: "StockMovements");

            migrationBuilder.DropTable(
                name: "StockRecipeLines");

            migrationBuilder.DropTable(
                name: "StockTransferLines");

            migrationBuilder.DropTable(
                name: "StockTransfers");

            migrationBuilder.DropTable(
                name: "StockOperations");

            migrationBuilder.DropTable(
                name: "StockRecipes");

            migrationBuilder.DropTable(
                name: "StockItems");
        }
    }
}
