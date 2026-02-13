using ConsertaPraMim.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ConsertaPraMim.Tests.Unit.Integration.Infrastructure;

internal static class InfrastructureTestDbContextFactory
{
    public static (ConsertaPraMimDbContext Context, SqliteConnection Connection) CreateSqliteContext()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<ConsertaPraMimDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new ConsertaPraMimDbContext(options);
        context.Database.EnsureCreated();
        return (context, connection);
    }

    public static ConsertaPraMimDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ConsertaPraMimDbContext>()
            .UseInMemoryDatabase($"ConsertaPraMimTests-{Guid.NewGuid()}")
            .Options;

        var context = new ConsertaPraMimDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
