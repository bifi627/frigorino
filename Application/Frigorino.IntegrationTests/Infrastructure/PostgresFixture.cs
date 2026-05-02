using Npgsql;
using Testcontainers.PostgreSql;

namespace Frigorino.IntegrationTests.Infrastructure;

public sealed class PostgresFixture : IAsyncDisposable
{
    public static readonly PostgresFixture Instance = new();

    private PostgreSqlContainer? _container;
    private string? _masterConnectionString;

    private PostgresFixture() { }

    public async Task StartAsync()
    {
        _container = new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("postgres")
            .WithUsername("postgres")
            .WithPassword("postgres_test")
            .Build();

        await _container.StartAsync();
        _masterConnectionString = _container.GetConnectionString();
    }

    public async Task CreateDatabaseAsync(string dbName)
    {
        await using var conn = new NpgsqlConnection(_masterConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand($"CREATE DATABASE \"{dbName}\"", conn);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DropDatabaseAsync(string dbName)
    {
        await using var conn = new NpgsqlConnection(_masterConnectionString);
        await conn.OpenAsync();

        // Terminate active connections before dropping
        await using var terminateCmd = new NpgsqlCommand(
            $"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '{dbName}' AND pid <> pg_backend_pid()",
            conn);
        await terminateCmd.ExecuteNonQueryAsync();

        await using var dropCmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{dbName}\"", conn);
        await dropCmd.ExecuteNonQueryAsync();
    }

    public string ConnectionStringFor(string dbName)
    {
        var builder = new NpgsqlConnectionStringBuilder(_masterConnectionString!) { Database = dbName };
        return builder.ToString();
    }

    public async ValueTask DisposeAsync()
    {
        if (_container != null)
            await _container.DisposeAsync();
    }
}
