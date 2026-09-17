using InfinitoCoffee.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InfinitoCoffee.Api.IntegrationTests.Persistence;

internal sealed class SqliteInMemoryDbContextFactory : IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteInMemoryDbContextFactory()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
    }

    public InfinitoCoffeeDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<InfinitoCoffeeDbContext>()
            .UseSqlite(_connection)
            .EnableSensitiveDataLogging()
            .Options;

        var dbContext = new InfinitoCoffeeDbContext(options);
        dbContext.Database.EnsureCreated();

        return dbContext;
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
