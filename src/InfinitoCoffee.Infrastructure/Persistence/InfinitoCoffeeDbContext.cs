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

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new OrderConfiguration());
        modelBuilder.ApplyConfiguration(new OrderItemConfiguration());
        modelBuilder.ApplyConfiguration(new ProductConfiguration());
        modelBuilder.ApplyConfiguration(new ProductCategoryConfiguration());
        modelBuilder.ApplyConfiguration(new UserConfiguration());

        base.OnModelCreating(modelBuilder);
    }
}
