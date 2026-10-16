using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfinitoCoffee.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderVoiding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReversesOperationId",
                table: "StockOperations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VoidedAtUtc",
                table: "Orders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockOperations_ReversesOperationId",
                table: "StockOperations",
                column: "ReversesOperationId",
                unique: true,
                filter: "[ReversesOperationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_BranchId_CreatedAtUtc_Id",
                table: "Orders",
                columns: new[] { "BranchId", "CreatedAtUtc", "Id" });

            migrationBuilder.AddForeignKey(
                name: "FK_StockOperations_StockOperations_ReversesOperationId",
                table: "StockOperations",
                column: "ReversesOperationId",
                principalTable: "StockOperations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StockOperations_StockOperations_ReversesOperationId",
                table: "StockOperations");

            migrationBuilder.DropIndex(
                name: "IX_StockOperations_ReversesOperationId",
                table: "StockOperations");

            migrationBuilder.DropIndex(
                name: "IX_Orders_BranchId_CreatedAtUtc_Id",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ReversesOperationId",
                table: "StockOperations");

            migrationBuilder.DropColumn(
                name: "VoidedAtUtc",
                table: "Orders");
        }
    }
}
