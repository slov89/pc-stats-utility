using Npgsql;
using Slov89.PCStats.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Slov89.PCStats.Data;

public class DatabaseService : IDatabaseService
{
    private readonly string _connectionString;
    private readonly ILogger<DatabaseService> _logger;

    public DatabaseService(IConfiguration configuration, ILogger<DatabaseService> logger)
    {
        _connectionString = Environment.GetEnvironmentVariable("slov89_pc_stats_utility_pg") 
            ?? throw new InvalidOperationException("slov89_pc_stats_utility_pg environment variable not set");
        _logger = logger;
    }

    /// <summary>
    /// Constructor for testing that accepts connection string directly
    /// </summary>
    public DatabaseService(string connectionString, ILogger<DatabaseService> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
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

    public async Task<int> CleanupOldSnapshotsAsync(int daysToKeep)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = "SELECT cleanup_old_snapshots(@daysToKeep)";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("daysToKeep", daysToKeep);

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
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

    public async Task<bool> IsConnectionAvailableAsync()
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<long> RestoreOfflineSnapshotAsync(OfflineSnapshotData snapshotData)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = @"
            INSERT INTO snapshots (snapshot_timestamp, total_cpu_usage, total_memory_usage_mb, total_available_memory_mb)
            VALUES (@timestamp, @totalCpuUsage, @totalMemoryMb, @availableMemoryMb)
            RETURNING snapshot_id";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("timestamp", snapshotData.Timestamp);
        command.Parameters.AddWithValue("totalCpuUsage", (object?)snapshotData.TotalCpuUsage ?? DBNull.Value);
        command.Parameters.AddWithValue("totalMemoryMb", (object?)snapshotData.TotalMemoryMb ?? DBNull.Value);
        command.Parameters.AddWithValue("availableMemoryMb", (object?)snapshotData.AvailableMemoryMb ?? DBNull.Value);

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt64(result);
    }

    public async Task<int> RestoreOfflineProcessAsync(OfflineProcessData processData)
    {
        return await GetOrCreateProcessAsync(processData.ProcessName, processData.ProcessPath);
    }

    public async Task RestoreOfflineSnapshotBatchAsync(OfflineSnapshotBatch batch)
    {
        if (batch.SnapshotData == null)
        {
            _logger.LogWarning("Cannot restore batch {BatchId} - no snapshot data", batch.BatchId);
            return;
        }

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            // 1. Restore the main snapshot
            var realSnapshotId = await RestoreOfflineSnapshotForBatch(batch.SnapshotData, connection, transaction);
            
            _logger.LogInformation("Restored offline snapshot {LocalSnapshotId} as database snapshot {RealSnapshotId}", 
                batch.LocalSnapshotId, realSnapshotId);

            // 2. Restore process snapshots
            foreach (var processSnapshot in batch.ProcessSnapshots)
            {
                try
                {
                    // Get or create the process
                    var processId = await RestoreOfflineProcessForBatch(processSnapshot, connection, transaction);
                    
                    // Create the process snapshot
                    await RestoreOfflineProcessSnapshotForBatch(realSnapshotId, processId, processSnapshot.ProcessInfo, connection, transaction);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to restore process snapshot for {ProcessName} in batch {BatchId}", 
                        processSnapshot.ProcessName, batch.BatchId);
                }
            }

            // 3. Restore CPU temperature if available
            if (batch.CpuTemperature != null)
            {
                try
                {
                    await RestoreOfflineCpuTemperatureForBatch(realSnapshotId, batch.CpuTemperature.Temperature, connection, transaction);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to restore CPU temperature in batch {BatchId}", batch.BatchId);
                }
            }

            await transaction.CommitAsync();
            
            _logger.LogInformation("Successfully restored offline snapshot batch {BatchId} with {ProcessCount} processes", 
                batch.BatchId, batch.ProcessSnapshots.Count);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task<long> RestoreOfflineSnapshotForBatch(OfflineSnapshotData snapshotData, NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        const string sql = @"
            INSERT INTO snapshots (snapshot_timestamp, total_cpu_usage, total_memory_usage_mb, total_available_memory_mb)
            VALUES (@timestamp, @totalCpuUsage, @totalMemoryMb, @availableMemoryMb)
            RETURNING snapshot_id";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("timestamp", snapshotData.Timestamp);
        command.Parameters.AddWithValue("totalCpuUsage", (object?)snapshotData.TotalCpuUsage ?? DBNull.Value);
        command.Parameters.AddWithValue("totalMemoryMb", (object?)snapshotData.TotalMemoryMb ?? DBNull.Value);
        command.Parameters.AddWithValue("availableMemoryMb", (object?)snapshotData.AvailableMemoryMb ?? DBNull.Value);

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt64(result);
    }

    private async Task<int> RestoreOfflineProcessForBatch(OfflineProcessSnapshotData processData, NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        // Try to get existing process
        const string selectSql = @"
            SELECT process_id FROM processes 
            WHERE process_name = @processName 
            AND (process_path = @processPath OR (process_path IS NULL AND @processPath IS NULL))";

        await using var selectCommand = new NpgsqlCommand(selectSql, connection, transaction);
        selectCommand.Parameters.AddWithValue("processName", processData.ProcessName);
        selectCommand.Parameters.AddWithValue("processPath", (object?)processData.ProcessPath ?? DBNull.Value);

        var existingId = await selectCommand.ExecuteScalarAsync();
        if (existingId != null)
        {
            // Update last seen
            const string updateSql = "UPDATE processes SET last_seen = NOW() WHERE process_id = @processId";
            await using var updateCommand = new NpgsqlCommand(updateSql, connection, transaction);
            updateCommand.Parameters.AddWithValue("processId", existingId);
            await updateCommand.ExecuteNonQueryAsync();

            return Convert.ToInt32(existingId);
        }

        // Create new process
        const string insertSql = @"
            INSERT INTO processes (process_name, process_path, first_seen, last_seen)
            VALUES (@processName, @processPath, NOW(), NOW())
            RETURNING process_id";

        await using var insertCommand = new NpgsqlCommand(insertSql, connection, transaction);
        insertCommand.Parameters.AddWithValue("processName", processData.ProcessName);
        insertCommand.Parameters.AddWithValue("processPath", (object?)processData.ProcessPath ?? DBNull.Value);

        var result = await insertCommand.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    private async Task RestoreOfflineProcessSnapshotForBatch(long snapshotId, int processId, ProcessInfo processInfo, NpgsqlConnection connection, NpgsqlTransaction transaction)
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

    private async Task RestoreOfflineCpuTemperatureForBatch(long snapshotId, CpuTemperature temperature, NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        const string sql = @"
            INSERT INTO cpu_temperatures (snapshot_id, cpu_tctl_tdie, cpu_die_average, cpu_ccd1_tdie, cpu_ccd2_tdie, thermal_limit_percent, thermal_throttling)
            VALUES (@snapshotId, @cpuTctlTdie, @cpuDieAverage, @cpuCcd1Tdie, @cpuCcd2Tdie, @thermalLimitPercent, @thermalThrottling)";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("snapshotId", snapshotId);
        command.Parameters.AddWithValue("cpuTctlTdie", (object?)temperature.CpuTctlTdie ?? DBNull.Value);
        command.Parameters.AddWithValue("cpuDieAverage", (object?)temperature.CpuDieAverage ?? DBNull.Value);
        command.Parameters.AddWithValue("cpuCcd1Tdie", (object?)temperature.CpuCcd1Tdie ?? DBNull.Value);
        command.Parameters.AddWithValue("cpuCcd2Tdie", (object?)temperature.CpuCcd2Tdie ?? DBNull.Value);
        command.Parameters.AddWithValue("thermalLimitPercent", (object?)temperature.ThermalLimitPercent ?? DBNull.Value);
        command.Parameters.AddWithValue("thermalThrottling", (object?)temperature.ThermalThrottling ?? DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }
}
