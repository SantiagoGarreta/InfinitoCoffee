using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfinitoCoffee.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemUserSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSystemUser",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "UX_Users_SingleSystemUser",
                table: "Users",
                column: "IsSystemUser",
                unique: true,
                filter: "[IsSystemUser] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Users_SingleSystemUser",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsSystemUser",
                table: "Users");
        }
    }
}
