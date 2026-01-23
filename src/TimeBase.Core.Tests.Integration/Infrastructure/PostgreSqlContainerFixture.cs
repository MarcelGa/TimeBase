using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;

namespace TimeBase.Core.Tests.Integration.Infrastructure;

public class PostgreSqlContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;
    private Respawner? _respawner;

    public PostgreSqlContainerFixture()
    {
        _container = new PostgreSqlBuilder("timescale/timescaledb:latest-pg16")
            .WithDatabase("timebase_test")
            .WithUsername("test_user")
            .WithPassword("test_password")
            .WithPortBinding(5432, true)
            .WithCleanUp(true)
            .Build();
    }

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        // Don't initialize Respawner here - will be done after migrations run
    }

    public async Task EnsureRespawnerInitializedAsync()
    {
        if (_respawner != null)
            return;

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore = ["__EFMigrationsHistory"]
        });
    }

    public async Task ResetDatabaseAsync()
    {
        await EnsureRespawnerInitializedAsync();

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await _respawner!.ResetAsync(connection);
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
