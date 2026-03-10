using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;
using Xunit;

namespace PinkRooster.Api.Tests.Fixtures;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17")
        .Build();

    private Respawner? _respawner;
    private bool _databaseInitialized;

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
    }

    public async Task EnsureMigratedAsync(ApiFactory factory)
    {
        if (_databaseInitialized)
            return;

        await factory.EnsureDatabaseCreatedAsync();
        await InitializeRespawnAsync();
        _databaseInitialized = true;
    }

    public async Task ResetDatabaseAsync()
    {
        if (_respawner is null)
            return;

        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        await _respawner.ResetAsync(conn);
    }

    private async Task InitializeRespawnAsync()
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        _respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            SchemasToInclude = ["public"],
            DbAdapter = DbAdapter.Postgres,
            TablesToIgnore = ["__EFMigrationsHistory"]
        });
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
