using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace COA.Infrastructure.ProfilePersistence;

public sealed class CoaDesignTimeDbContextFactory : IDesignTimeDbContextFactory<CoaDbContext>
{
    public const string ConnectionStringEnvironmentVariable = "COA_SQL_CONNECTION_STRING";

    public CoaDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Set {ConnectionStringEnvironmentVariable} before running EF Core design-time commands.");
        }

        var options = new DbContextOptionsBuilder<CoaDbContext>()
            .UseSqlServer(connectionString, sql =>
                sql.MigrationsAssembly(typeof(CoaDbContext).Assembly.GetName().Name))
            .Options;

        return new CoaDbContext(options);
    }
}
