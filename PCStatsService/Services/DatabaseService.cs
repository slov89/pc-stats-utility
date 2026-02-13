using Npgsql;
using PCStatsService.Models;

namespace PCStatsService.Services;

public interface IDatabaseService
{
    Task InitializeAsync();
    Task<long> CreateSnapshotAsync(decimal? totalCpuUsage, long? totalMemoryMb, long? availableMemoryMb);
    Task<int> GetOrCreateProcessAsync(string processName, string? processPath);
    Task CreateProcessSnapshotAsync(long snapshotId, int processId, ProcessInfo processInfo);
    Task CreateCpuTemperatureAsync(long snapshotId, CpuTemperature temperature);
    Task TestConnectionAsync();
}

public class DatabaseService : IDatabaseService
{
    private readonly string _connectionString;
    private readonly ILogger<DatabaseService> _logger;

    public DatabaseService(IConfiguration configuration, ILogger<DatabaseService> logger)
    {
        _connectionString = configuration.GetConnectionString("PostgreSQL") 
            ?? throw new InvalidOperationException("PostgreSQL connection string not configured");
        _logger = logger;
    }

    public async Task TestConnectionAsync()
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            _logger.LogInformation("Successfully connected to PostgreSQL database");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to PostgreSQL database");
            throw;
        }
    }

    public async Task InitializeAsync()
    {
        await TestConnectionAsync();
    }

    public async Task<long> CreateSnapshotAsync(decimal? totalCpuUsage, long? totalMemoryMb, long? availableMemoryMb)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = @"
            INSERT INTO snapshots (snapshot_timestamp, total_cpu_usage, total_memory_usage_mb, total_available_memory_mb)
            VALUES (NOW(), @totalCpuUsage, @totalMemoryMb, @availableMemoryMb)
            RETURNING snapshot_id";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("totalCpuUsage", (object?)totalCpuUsage ?? DBNull.Value);
        command.Parameters.AddWithValue("totalMemoryMb", (object?)totalMemoryMb ?? DBNull.Value);
        command.Parameters.AddWithValue("availableMemoryMb", (object?)availableMemoryMb ?? DBNull.Value);

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt64(result);
    }

    public async Task<int> GetOrCreateProcessAsync(string processName, string? processPath)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        // Try to get existing process
        const string selectSql = @"
            SELECT process_id FROM processes 
            WHERE process_name = @processName 
            AND (process_path = @processPath OR (process_path IS NULL AND @processPath IS NULL))";

        await using var selectCommand = new NpgsqlCommand(selectSql, connection);
        selectCommand.Parameters.AddWithValue("processName", processName);
        selectCommand.Parameters.AddWithValue("processPath", (object?)processPath ?? DBNull.Value);

        var existingId = await selectCommand.ExecuteScalarAsync();
        if (existingId != null)
        {
            // Update last seen
            const string updateSql = "UPDATE processes SET last_seen = NOW() WHERE process_id = @processId";
            await using var updateCommand = new NpgsqlCommand(updateSql, connection);
            updateCommand.Parameters.AddWithValue("processId", existingId);
            await updateCommand.ExecuteNonQueryAsync();

            return Convert.ToInt32(existingId);
        }

        // Create new process
        const string insertSql = @"
            INSERT INTO processes (process_name, process_path, first_seen, last_seen)
            VALUES (@processName, @processPath, NOW(), NOW())
            RETURNING process_id";

        await using var insertCommand = new NpgsqlCommand(insertSql, connection);
        insertCommand.Parameters.AddWithValue("processName", processName);
        insertCommand.Parameters.AddWithValue("processPath", (object?)processPath ?? DBNull.Value);

        var result = await insertCommand.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    public async Task CreateProcessSnapshotAsync(long snapshotId, int processId, ProcessInfo processInfo)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = @"
            INSERT INTO process_snapshots (
                snapshot_id, process_id, pid, cpu_usage, memory_usage_mb, 
                private_memory_mb, virtual_memory_mb, vram_usage_mb, 
                thread_count, handle_count
            )
            VALUES (
                @snapshotId, @processId, @pid, @cpuUsage, @memoryUsageMb,
                @privateMemoryMb, @virtualMemoryMb, @vramUsageMb,
                @threadCount, @handleCount
            )";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("snapshotId", snapshotId);
        command.Parameters.AddWithValue("processId", processId);
        command.Parameters.AddWithValue("pid", processInfo.Pid);
        command.Parameters.AddWithValue("cpuUsage", processInfo.CpuUsage);
        command.Parameters.AddWithValue("memoryUsageMb", processInfo.MemoryUsageMb);
        command.Parameters.AddWithValue("privateMemoryMb", processInfo.PrivateMemoryMb);
        command.Parameters.AddWithValue("virtualMemoryMb", processInfo.VirtualMemoryMb);
        command.Parameters.AddWithValue("vramUsageMb", (object?)processInfo.VramUsageMb ?? DBNull.Value);
        command.Parameters.AddWithValue("threadCount", processInfo.ThreadCount);
        command.Parameters.AddWithValue("handleCount", processInfo.HandleCount);

        await command.ExecuteNonQueryAsync();
    }

    public async Task CreateCpuTemperatureAsync(long snapshotId, CpuTemperature temperature)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = @"
            INSERT INTO cpu_temperatures (snapshot_id, cpu_tctl_tdie, cpu_die_average, cpu_ccd1_tdie, cpu_ccd2_tdie, thermal_limit_percent, thermal_throttling)
            VALUES (@snapshotId, @cpuTctlTdie, @cpuDieAverage, @cpuCcd1Tdie, @cpuCcd2Tdie, @thermalLimitPercent, @thermalThrottling)";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("snapshotId", snapshotId);
        command.Parameters.AddWithValue("cpuTctlTdie", (object?)temperature.CpuTctlTdie ?? DBNull.Value);
        command.Parameters.AddWithValue("cpuDieAverage", (object?)temperature.CpuDieAverage ?? DBNull.Value);
        command.Parameters.AddWithValue("cpuCcd1Tdie", (object?)temperature.CpuCcd1Tdie ?? DBNull.Value);
        command.Parameters.AddWithValue("cpuCcd2Tdie", (object?)temperature.CpuCcd2Tdie ?? DBNull.Value);
        command.Parameters.AddWithValue("thermalLimitPercent", (object?)temperature.ThermalLimitPercent ?? DBNull.Value);
        command.Parameters.AddWithValue("thermalThrottling", (object?)temperature.ThermalThrottling ?? DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }

    public async Task BatchCreateProcessSnapshotsAsync(long snapshotId, List<(int processId, ProcessInfo processInfo)> processSnapshots)
    {
        if (!processSnapshots.Any())
            return;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            const string sql = @"
                INSERT INTO process_snapshots (
                    snapshot_id, process_id, pid, cpu_usage, memory_usage_mb, 
                    private_memory_mb, virtual_memory_mb, vram_usage_mb, 
                    thread_count, handle_count
                )
                VALUES (
                    @snapshotId, @processId, @pid, @cpuUsage, @memoryUsageMb,
                    @privateMemoryMb, @virtualMemoryMb, @vramUsageMb,
                    @threadCount, @handleCount
                )";

            foreach (var (processId, processInfo) in processSnapshots)
            {
                await using var command = new NpgsqlCommand(sql, connection, transaction);
                command.Parameters.AddWithValue("snapshotId", snapshotId);
                command.Parameters.AddWithValue("processId", processId);
                command.Parameters.AddWithValue("pid", processInfo.Pid);
                command.Parameters.AddWithValue("cpuUsage", processInfo.CpuUsage);
                command.Parameters.AddWithValue("memoryUsageMb", processInfo.MemoryUsageMb);
                command.Parameters.AddWithValue("privateMemoryMb", processInfo.PrivateMemoryMb);
                command.Parameters.AddWithValue("virtualMemoryMb", processInfo.VirtualMemoryMb);
                command.Parameters.AddWithValue("vramUsageMb", (object?)processInfo.VramUsageMb ?? DBNull.Value);
                command.Parameters.AddWithValue("threadCount", processInfo.ThreadCount);
                command.Parameters.AddWithValue("handleCount", processInfo.HandleCount);

                await command.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
