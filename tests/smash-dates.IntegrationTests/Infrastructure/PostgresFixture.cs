using smash_dates.Migrations;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;

namespace smash_dates.IntegrationTests.Infrastructure;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("smash_dates")
        .WithUsername("postgres")
        .WithPassword("postgres")
        // Each test spins up its own WebApplicationFactory (and Npgsql pool); the default
        // server cap of 100 is exhausted once the whole suite runs back-to-back.
        .WithCommand("-c", "max_connections=500")
        .Build();

    private Respawner? _respawner;

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        DbMigrator.Apply(ConnectionString);

        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        _respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore =
            [
                new Respawn.Graph.Table("public", "schemaversions"),
                new Respawn.Graph.Table("public", "data_protection_keys"),
            ],
        });
    }

    public async Task ResetAsync()
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        await _respawner!.ResetAsync(conn);
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
