using InfinitoCoffee.Domain.Orders;
using InfinitoCoffee.Domain.Products;
using InfinitoCoffee.Domain.Users;
using InfinitoCoffee.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace InfinitoCoffee.Infrastructure.Persistence;

public class InfinitoCoffeeDbContext : DbContext
{
    public InfinitoCoffeeDbContext(DbContextOptions<InfinitoCoffeeDbContext> options)
        : base(options)
    {
    }

    public DbSet<Domain.Branches.Branch> Branches => Set<Domain.Branches.Branch>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Domain.Stock.StockItem> StockItems => Set<Domain.Stock.StockItem>();
    public DbSet<Domain.Stock.StockBalance> StockBalances => Set<Domain.Stock.StockBalance>();
    public DbSet<Domain.Stock.StockRecipe> StockRecipes => Set<Domain.Stock.StockRecipe>();
    public DbSet<Domain.Stock.StockOperation> StockOperations => Set<Domain.Stock.StockOperation>();
    public DbSet<Domain.Stock.StockTransfer> StockTransfers => Set<Domain.Stock.StockTransfer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new OrderConfiguration());
        modelBuilder.ApplyConfiguration(new OrderItemConfiguration());
        modelBuilder.ApplyConfiguration(new ProductConfiguration());
        modelBuilder.ApplyConfiguration(new ProductCategoryConfiguration());
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        StockConfiguration.Configure(modelBuilder);
        var branch = modelBuilder.Entity<Domain.Branches.Branch>();
        branch.ToTable("Branches");
        branch.HasKey(x => x.Id);
        branch.Property(x => x.Name).HasMaxLength(100).IsRequired();
        branch.HasData(new Domain.Branches.Branch { Id = 1, Name = "Sucursal 1" },
            new Domain.Branches.Branch { Id = 2, Name = "Sucursal 2" });

        base.OnModelCreating(modelBuilder);
    }
}
