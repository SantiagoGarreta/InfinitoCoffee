using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfinitoCoffee.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RegisterExistingProductsInStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM Products p
                    WHERE p.IsDeleted = 0
                      AND NOT EXISTS (SELECT 1 FROM StockItems s WHERE s.ProductId = p.Id)
                      AND EXISTS (SELECT 1 FROM StockItems s WHERE s.NormalizedName = UPPER(p.Name) AND s.IsDeleted = 0)
                )
                    THROW 50000, 'An existing stock item uses the name of a product without stock. Resolve the name before migrating.', 1;

                IF EXISTS (
                    SELECT 1 FROM Products p
                    WHERE p.IsDeleted = 0
                      AND NOT EXISTS (SELECT 1 FROM StockItems s WHERE s.ProductId = p.Id)
                    GROUP BY UPPER(p.Name)
                    HAVING COUNT(*) > 1
                )
                    THROW 50000, 'Products without stock have duplicate names. Resolve them before migrating.', 1;

                DECLARE @Created TABLE (Id uniqueidentifier NOT NULL);

                INSERT INTO StockItems (Id, Name, NormalizedName, Kind, Unit, ProductId, MinimumQuantity, IsDeleted)
                OUTPUT inserted.Id INTO @Created (Id)
                SELECT NEWID(), p.Name, UPPER(p.Name), 2, 1, p.Id, 0, 0
                FROM Products p
                WHERE p.IsDeleted = 0
                  AND NOT EXISTS (SELECT 1 FROM StockItems s WHERE s.ProductId = p.Id);

                INSERT INTO StockBalances (ItemId, BranchId, Location, Quantity, Revision)
                SELECT created.Id, branch.Id, 1, 0, NEWID()
                FROM @Created created CROSS JOIN Branches branch;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Stock items may have recipes and movements by rollback time. Keep their data.
        }
    }
}
