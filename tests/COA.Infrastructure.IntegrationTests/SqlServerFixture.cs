using COA.Infrastructure.ProfilePersistence;
using COA.Infrastructure.OpportunityPersistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace COA.Infrastructure.IntegrationTests;

public sealed class SqlServerFixture : IAsyncLifetime
{
    public const string ConnectionStringEnvironmentVariable = "COA_SQL_TEST_CONNECTION_STRING";

    private string _masterConnectionString = string.Empty;

    public string DatabaseName { get; private set; } = string.Empty;

    public string ConnectionString { get; private set; } = string.Empty;

    public IDbContextFactory<CoaDbContext> ContextFactory { get; private set; } = null!;

    public SqlServerProfileStore CreateStore() => new(ContextFactory);

    public SqlServerOpportunityStore CreateOpportunityStore() => new(ContextFactory);

    public async Task InitializeAsync()
    {
        var configuredConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(configuredConnectionString))
        {
            throw new InvalidOperationException(
                $"{ConnectionStringEnvironmentVariable} is required; SQL integration tests never skip silently.");
        }

        var masterBuilder = new SqlConnectionStringBuilder(configuredConnectionString)
        {
            InitialCatalog = "master"
        };
        _masterConnectionString = masterBuilder.ConnectionString;
        await WaitForSqlServerAsync(_masterConnectionString);

        DatabaseName = $"COA_Integration_{Guid.NewGuid():N}";
        await using (var connection = new SqlConnection(_masterConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE [{DatabaseName}]";
            await command.ExecuteNonQueryAsync();
        }

        var databaseBuilder = new SqlConnectionStringBuilder(configuredConnectionString)
        {
            InitialCatalog = DatabaseName
        };
        ConnectionString = databaseBuilder.ConnectionString;
        var options = new DbContextOptionsBuilder<CoaDbContext>()
            .UseSqlServer(ConnectionString, sql =>
                sql.MigrationsAssembly(typeof(CoaDbContext).Assembly.GetName().Name))
            .Options;
        ContextFactory = new TestDbContextFactory(options);

        await using var context = await ContextFactory.CreateDbContextAsync();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrEmpty(DatabaseName) || string.IsNullOrEmpty(_masterConnectionString))
            return;

        await using var connection = new SqlConnection(_masterConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            IF DB_ID(N'{DatabaseName}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{DatabaseName}];
            END
            """;
        await command.ExecuteNonQueryAsync();
    }

    public async Task ExecuteSqlAsync(string sql)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task WaitForSqlServerAsync(string connectionString)
    {
        Exception? lastException = null;
        for (var attempt = 0; attempt < 30; attempt++)
        {
            try
            {
                await using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();
                return;
            }
            catch (SqlException exception)
            {
                lastException = exception;
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
        }

        throw new InvalidOperationException("SQL Server was unavailable after the readiness timeout.", lastException);
    }

    private sealed class TestDbContextFactory(DbContextOptions<CoaDbContext> options)
        : IDbContextFactory<CoaDbContext>
    {
        public CoaDbContext CreateDbContext() => new(options);

        public Task<CoaDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}
