using InfinitoCoffee.Domain.Orders;
using InfinitoCoffee.Domain.Products;
using InfinitoCoffee.Domain.Stock;
using InfinitoCoffee.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace InfinitoCoffee.Infrastructure.Persistence.Configurations;

internal static class StockConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        var item = model.Entity<StockItem>();
        item.ToTable("StockItems");
        item.HasKey(x => x.Id);
        item.Property(x => x.Name).HasMaxLength(150).IsRequired();
        item.Property(x => x.NormalizedName).HasMaxLength(150).IsRequired();
        item.HasIndex(x => x.NormalizedName).IsUnique();
        item.Property(x => x.MinimumQuantity).HasPrecision(18, 3);
        item.HasIndex(x => x.ProductId).IsUnique().HasFilter("[ProductId] IS NOT NULL");
        item.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);

        var balance = model.Entity<StockBalance>();
        balance.ToTable("StockBalances", t => t.HasCheckConstraint("CK_StockBalances_Quantity", "[Quantity] >= 0"));
        balance.HasKey(x => new { x.ItemId, x.Location });
        balance.Property(x => x.Quantity).HasPrecision(18, 3);
        balance.Property(x => x.Revision).IsConcurrencyToken();
        balance.HasOne<StockItem>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);

        var recipe = model.Entity<StockRecipe>();
        recipe.ToTable("StockRecipes");
        recipe.HasKey(x => x.Id);
        recipe.Property(x => x.Yield).HasPrecision(18, 3);
        recipe.HasIndex(x => new { x.ItemId, x.Version }).IsUnique();
        recipe.HasOne<StockItem>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        recipe.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        recipe.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Cascade);
        var recipeLine = model.Entity<StockRecipeLine>();
        recipeLine.ToTable("StockRecipeLines");
        recipeLine.HasKey(x => x.Id);
        recipeLine.Property(x => x.Quantity).HasPrecision(18, 3);
        recipeLine.HasIndex(x => new { x.RecipeId, x.ItemId }).IsUnique();
        recipeLine.HasOne<StockItem>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);

        var operation = model.Entity<StockOperation>();
        operation.ToTable("StockOperations");
        operation.HasKey(x => x.Id);
        operation.Property(x => x.Type).HasMaxLength(32).IsRequired();
        operation.Property(x => x.RequestHash).HasMaxLength(64).IsRequired();
        operation.Property(x => x.Notes).HasMaxLength(1000).IsRequired();
        operation.HasIndex(x => new { x.CreatedAtUtc, x.Id });
        operation.HasIndex(x => x.OrderId).IsUnique().HasFilter("[OrderId] IS NOT NULL");
        operation.HasOne<StockRecipe>().WithMany().HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Restrict);
        operation.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        operation.HasOne<User>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
        operation.HasMany(x => x.Movements).WithOne().HasForeignKey(x => x.OperationId).OnDelete(DeleteBehavior.Cascade);
        var movement = model.Entity<StockMovement>();
        movement.ToTable("StockMovements");
        movement.HasKey(x => x.Id);
        movement.Property(x => x.Before).HasPrecision(18, 3);
        movement.Property(x => x.Delta).HasPrecision(18, 3);
        movement.Property(x => x.After).HasPrecision(18, 3);
        movement.HasOne<StockItem>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        movement.HasIndex(x => new { x.ItemId, x.Location });

        var transfer = model.Entity<StockTransfer>();
        transfer.ToTable("StockTransfers");
        transfer.HasKey(x => x.Id);
        transfer.Property(x => x.Revision).IsConcurrencyToken();
        transfer.HasIndex(x => x.ReceivedAtUtc);
        transfer.HasOne<StockOperation>().WithMany().HasForeignKey(x => x.Id).OnDelete(DeleteBehavior.Restrict);
        transfer.HasOne<StockOperation>().WithMany().HasForeignKey(x => x.ReceiptOperationId).OnDelete(DeleteBehavior.Restrict);
        transfer.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.TransferId).OnDelete(DeleteBehavior.Cascade);
        var transferLine = model.Entity<StockTransferLine>();
        transferLine.ToTable("StockTransferLines");
        transferLine.HasKey(x => x.Id);
        transferLine.Property(x => x.Sent).HasPrecision(18, 3);
        transferLine.Property(x => x.Received).HasPrecision(18, 3);
        transferLine.HasIndex(x => new { x.TransferId, x.ItemId }).IsUnique();
        transferLine.HasOne<StockItem>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
