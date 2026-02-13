using Npgsql;
using Testcontainers.PostgreSql;

namespace Slov89.PCStats.Data.Tests.Integration;

/// <summary>
/// Shared PostgreSQL container fixture for integration tests
/// Container is started once and shared across all test classes
/// </summary>
public class SharedPostgreSqlFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _postgresContainer;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        // Start PostgreSQL container once for all tests
        _postgresContainer = new PostgreSqlBuilder("postgres:15-alpine")
            .WithDatabase("pc_stats_integration_test")
            .WithUsername("test_user")
            .WithPassword("test_password")
            .Build();

        await _postgresContainer.StartAsync();
        
        ConnectionString = _postgresContainer.GetConnectionString();

        // Initialize base schema
        await InitializeDatabaseSchemaAsync();
    }

    public async Task DisposeAsync()
    {
        if (_postgresContainer != null)
        {
            await _postgresContainer.DisposeAsync();
        }
    }

    private async Task InitializeDatabaseSchemaAsync()
    {
        var schema = @"
            CREATE TABLE snapshots (
                snapshot_id BIGSERIAL PRIMARY KEY,
                snapshot_timestamp TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                total_cpu_usage DECIMAL(5,2),
                total_memory_usage_mb BIGINT,
                total_available_memory_mb BIGINT
            );

            CREATE INDEX idx_snapshots_timestamp ON snapshots(snapshot_timestamp DESC);

            CREATE TABLE processes (
                process_id SERIAL PRIMARY KEY,
                process_name VARCHAR(255) NOT NULL,
                process_path TEXT,
                first_seen TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                last_seen TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                CONSTRAINT uq_process_name_path UNIQUE(process_name, process_path)
            );

            CREATE INDEX idx_processes_name ON processes(process_name);
            CREATE INDEX idx_processes_last_seen ON processes(last_seen DESC);

            CREATE TABLE process_snapshots (
                process_snapshot_id BIGSERIAL PRIMARY KEY,
                snapshot_id BIGINT NOT NULL REFERENCES snapshots(snapshot_id) ON DELETE CASCADE,
                process_id INTEGER NOT NULL REFERENCES processes(process_id) ON DELETE CASCADE,
                pid INTEGER NOT NULL,
                cpu_usage DECIMAL(5,2),
                memory_usage_mb BIGINT,
                private_memory_mb BIGINT,
                virtual_memory_mb BIGINT,
                vram_usage_mb BIGINT,
                thread_count INTEGER,
                handle_count INTEGER
            );

            CREATE INDEX idx_process_snapshots_snapshot ON process_snapshots(snapshot_id);
            CREATE INDEX idx_process_snapshots_process ON process_snapshots(process_id);

            CREATE TABLE cpu_temperatures (
                temp_id BIGSERIAL PRIMARY KEY,
                snapshot_id BIGINT NOT NULL REFERENCES snapshots(snapshot_id) ON DELETE CASCADE,
                cpu_tctl_tdie DECIMAL(5,2),
                cpu_die_average DECIMAL(5,2),
                cpu_ccd1_tdie DECIMAL(5,2),
                cpu_ccd2_tdie DECIMAL(5,2),
                thermal_limit_percent DECIMAL(5,2),
                thermal_throttling BOOLEAN
            );

            CREATE INDEX idx_cpu_temps_snapshot ON cpu_temperatures(snapshot_id);";

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(schema, connection);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Cleans all data from tables between tests while preserving schema
    /// </summary>
    public async Task CleanDatabaseAsync()
    {
        var cleanupSql = @"
            TRUNCATE TABLE process_snapshots CASCADE;
            TRUNCATE TABLE cpu_temperatures CASCADE;
            TRUNCATE TABLE snapshots RESTART IDENTITY CASCADE;
            TRUNCATE TABLE processes RESTART IDENTITY CASCADE;";

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(cleanupSql, connection);
        await command.ExecuteNonQueryAsync();
    }
}

/// <summary>
/// Collection definition for sharing the PostgreSQL fixture across multiple test classes
/// </summary>
[CollectionDefinition("PostgreSQL Collection")]
public class PostgreSqlCollectionFixture : ICollectionFixture<SharedPostgreSqlFixture>
{
    // This class is never instantiated, it's just a marker for xUnit
}
