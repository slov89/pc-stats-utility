using Microsoft.Extensions.Logging;
using Npgsql;
using Slov89.PCStats.Models;

namespace Slov89.PCStats.Data;

public class MetricsService : IMetricsService
{
    private readonly string _connectionString;
    private readonly ILogger<MetricsService> _logger;

    public MetricsService(ILogger<MetricsService> logger)
    {
        _connectionString = Environment.GetEnvironmentVariable("slov89_pc_stats_utility_pg") 
            ?? throw new InvalidOperationException("slov89_pc_stats_utility_pg environment variable not set");
        _logger = logger;
    }

    /// <summary>
    /// Constructor for testing that accepts connection string directly
    /// </summary>
    public MetricsService(string connectionString, ILogger<MetricsService> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger;
    }

    public async Task<List<Snapshot>> GetSnapshotsAsync(DateTime startTime, DateTime endTime)
    {
        var snapshots = new List<Snapshot>();

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            // Convert local time parameters to UTC for database query
            var startTimeUtc = startTime.ToUniversalTime();
            var endTimeUtc = endTime.ToUniversalTime();

            const string sql = @"
                SELECT snapshot_id, snapshot_timestamp, total_cpu_usage, 
                       total_memory_usage_mb, total_available_memory_mb
                FROM snapshots
                WHERE snapshot_timestamp >= @startTime 
                  AND snapshot_timestamp <= @endTime
                ORDER BY snapshot_timestamp ASC";

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("startTime", startTimeUtc);
            command.Parameters.AddWithValue("endTime", endTimeUtc);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var timestamp = reader.GetDateTime(1);
                // Convert from UTC to local time for display
                var localTimestamp = DateTime.SpecifyKind(timestamp, DateTimeKind.Utc).ToLocalTime();
                
                snapshots.Add(new Snapshot
                {
                    SnapshotId = reader.GetInt64(0),
                    SnapshotTimestamp = localTimestamp,
                    TotalCpuUsage = reader.IsDBNull(2) ? null : reader.GetDecimal(2),
                    TotalMemoryUsageMb = reader.IsDBNull(3) ? null : reader.GetInt64(3),
                    TotalAvailableMemoryMb = reader.IsDBNull(4) ? null : reader.GetInt64(4)
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching snapshots from {StartTime} to {EndTime}", startTime, endTime);
        }

        return snapshots;
    }

    public async Task<List<CpuTemperature>> GetCpuTemperaturesAsync(DateTime startTime, DateTime endTime)
    {
        var temperatures = new List<CpuTemperature>();

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            // Convert local time parameters to UTC for database query
            var startTimeUtc = startTime.ToUniversalTime();
            var endTimeUtc = endTime.ToUniversalTime();

            const string sql = @"
                SELECT t.temp_id, t.snapshot_id, t.cpu_tctl_tdie, t.cpu_die_average,
                       t.cpu_ccd1_tdie, t.cpu_ccd2_tdie, t.thermal_limit_percent, 
                       t.thermal_throttling
                FROM cpu_temperatures t
                INNER JOIN snapshots s ON t.snapshot_id = s.snapshot_id
                WHERE s.snapshot_timestamp >= @startTime 
                  AND s.snapshot_timestamp <= @endTime
                ORDER BY s.snapshot_timestamp ASC";

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("startTime", startTimeUtc);
            command.Parameters.AddWithValue("endTime", endTimeUtc);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                temperatures.Add(new CpuTemperature
                {
                    TempId = reader.GetInt64(0),
                    SnapshotId = reader.GetInt64(1),
                    CpuTctlTdie = reader.IsDBNull(2) ? null : reader.GetDecimal(2),
                    CpuDieAverage = reader.IsDBNull(3) ? null : reader.GetDecimal(3),
                    CpuCcd1Tdie = reader.IsDBNull(4) ? null : reader.GetDecimal(4),
                    CpuCcd2Tdie = reader.IsDBNull(5) ? null : reader.GetDecimal(5),
                    ThermalLimitPercent = reader.IsDBNull(6) ? null : reader.GetDecimal(6),
                    ThermalThrottling = reader.IsDBNull(7) ? null : reader.GetBoolean(7)
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching CPU temperatures from {StartTime} to {EndTime}", startTime, endTime);
        }

        return temperatures;
    }

    public async Task<Dictionary<string, List<(DateTime timestamp, decimal cpuUsage, long memoryMb)>>> GetTopProcessesAsync(
        DateTime startTime, DateTime endTime, int topCount = 5)
    {
        var processMetrics = new Dictionary<string, List<(DateTime, decimal, long)>>();

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            // Convert local time parameters to UTC for database query
            var startTimeUtc = startTime.ToUniversalTime();
            var endTimeUtc = endTime.ToUniversalTime();

            // Get top processes by average CPU usage
            const string topProcessesSql = @"
                SELECT p.process_name, 
                       AVG(ps.cpu_usage) as avg_cpu
                FROM process_snapshots ps
                INNER JOIN processes p ON ps.process_id = p.process_id
                INNER JOIN snapshots s ON ps.snapshot_id = s.snapshot_id
                WHERE s.snapshot_timestamp >= @startTime 
                  AND s.snapshot_timestamp <= @endTime
                  AND ps.cpu_usage IS NOT NULL
                GROUP BY p.process_name
                ORDER BY avg_cpu DESC
                LIMIT @topCount";

            await using var topCommand = new NpgsqlCommand(topProcessesSql, connection);
            topCommand.Parameters.AddWithValue("startTime", startTimeUtc);
            topCommand.Parameters.AddWithValue("endTime", endTimeUtc);
            topCommand.Parameters.AddWithValue("topCount", topCount);

            var topProcessNames = new List<string>();
            await using (var reader = await topCommand.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    topProcessNames.Add(reader.GetString(0));
                }
            }

            // Get time series data for each top process
            foreach (var processName in topProcessNames)
            {
                const string metricsSql = @"
                    SELECT s.snapshot_timestamp,
                           ps.cpu_usage as cpu_usage,
                           ps.memory_usage_mb as memory_mb
                    FROM snapshots s
                    INNER JOIN process_snapshots ps ON s.snapshot_id = ps.snapshot_id
                    INNER JOIN processes p ON ps.process_id = p.process_id
                    WHERE p.process_name = @processName
                      AND s.snapshot_timestamp >= @startTime 
                      AND s.snapshot_timestamp <= @endTime
                    ORDER BY s.snapshot_timestamp ASC";

                await using var metricsCommand = new NpgsqlCommand(metricsSql, connection);
                metricsCommand.Parameters.AddWithValue("processName", processName);
                metricsCommand.Parameters.AddWithValue("startTime", startTimeUtc);
                metricsCommand.Parameters.AddWithValue("endTime", endTimeUtc);

                var metrics = new List<(DateTime, decimal, long)>();
                await using (var reader = await metricsCommand.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var timestamp = reader.GetDateTime(0);
                        // Convert from UTC to local time for display
                        var localTimestamp = DateTime.SpecifyKind(timestamp, DateTimeKind.Utc).ToLocalTime();
                        
                        metrics.Add((
                            localTimestamp,
                            reader.GetDecimal(1),
                            reader.GetInt64(2)
                        ));
                    }
                }

                processMetrics[processName] = metrics;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching top process metrics from {StartTime} to {EndTime}", startTime, endTime);
        }

        return processMetrics;
    }

    public async Task<Dictionary<long, List<ProcessSnapshotWithName>>> GetProcessSnapshotsAsync(DateTime startTime, DateTime endTime)
    {
        var result = new Dictionary<long, List<ProcessSnapshotWithName>>();

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            // Convert local time parameters to UTC for database query
            var startTimeUtc = startTime.ToUniversalTime();
            var endTimeUtc = endTime.ToUniversalTime();

            const string sql = @"
                SELECT ps.snapshot_id, 
                       p.process_name, 
                       SUM(ps.private_memory_mb) as private_memory_mb,
                       SUM(ps.memory_usage_mb) as memory_usage_mb,
                       SUM(ps.cpu_usage) as cpu_usage,
                       COUNT(*) as process_count
                FROM process_snapshots ps
                INNER JOIN processes p ON ps.process_id = p.process_id
                INNER JOIN snapshots s ON ps.snapshot_id = s.snapshot_id
                WHERE s.snapshot_timestamp >= @startTime 
                  AND s.snapshot_timestamp <= @endTime
                GROUP BY ps.snapshot_id, p.process_name
                ORDER BY ps.snapshot_id, SUM(ps.private_memory_mb) DESC NULLS LAST";

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("startTime", startTimeUtc);
            command.Parameters.AddWithValue("endTime", endTimeUtc);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var snapshotId = reader.GetInt64(0);
                var processSnapshot = new ProcessSnapshotWithName
                {
                    SnapshotId = snapshotId,
                    ProcessName = reader.GetString(1),
                    PrivateMemoryMb = reader.IsDBNull(2) ? null : reader.GetInt64(2),
                    MemoryUsageMb = reader.IsDBNull(3) ? null : reader.GetInt64(3),
                    CpuUsage = reader.IsDBNull(4) ? null : reader.GetDecimal(4),
                    ProcessCount = reader.GetInt32(5)
                };

                if (!result.ContainsKey(snapshotId))
                {
                    result[snapshotId] = new List<ProcessSnapshotWithName>();
                }

                result[snapshotId].Add(processSnapshot);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching process snapshots from {StartTime} to {EndTime}", startTime, endTime);
        }

        return result;
    }
}
