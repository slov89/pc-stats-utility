using PCStats.Models;

namespace PCStats.Data;

/// <summary>
/// Provides database operations for storing and retrieving PC statistics
/// </summary>
public interface IDatabaseService
{
    /// <summary>
    /// Initializes the database connection and verifies connectivity
    /// </summary>
    Task InitializeAsync();
    
    /// <summary>
    /// Creates a new system snapshot record
    /// </summary>
    /// <param name="totalCpuUsage">The total CPU usage percentage</param>
    /// <param name="totalMemoryMb">The total memory usage in megabytes</param>
    /// <param name="availableMemoryMb">The available memory in megabytes</param>
    /// <returns>The ID of the created snapshot</returns>
    Task<long> CreateSnapshotAsync(decimal? totalCpuUsage, long? totalMemoryMb, long? availableMemoryMb);
    
    /// <summary>
    /// Gets or creates a process record
    /// </summary>
    /// <param name="processName">The name of the process</param>
    /// <param name="processPath">The file path of the process executable</param>
    /// <returns>The ID of the process record</returns>
    Task<int> GetOrCreateProcessAsync(string processName, string? processPath);
    
    /// <summary>
    /// Creates a process snapshot record
    /// </summary>
    /// <param name="snapshotId">The snapshot ID this process snapshot belongs to</param>
    /// <param name="processId">The process ID</param>
    /// <param name="processInfo">The detailed process information</param>
    Task CreateProcessSnapshotAsync(long snapshotId, int processId, ProcessInfo processInfo);
    
    /// <summary>
    /// Creates a CPU temperature record
    /// </summary>
    /// <param name="snapshotId">The snapshot ID this temperature reading belongs to</param>
    /// <param name="temperature">The temperature measurements</param>
    Task CreateCpuTemperatureAsync(long snapshotId, CpuTemperature temperature);
    
    /// <summary>
    /// Tests the database connection
    /// </summary>
    Task TestConnectionAsync();
    
    /// <summary>
    /// Removes snapshots older than the specified number of days
    /// </summary>
    /// <param name="daysToKeep">The number of days to retain</param>
    /// <returns>The number of snapshots deleted</returns>
    Task<int> CleanupOldSnapshotsAsync(int daysToKeep);
    
    /// <summary>
    /// Gets or creates multiple process records in a batch
    /// </summary>
    /// <param name="processes">The list of processes to get or create</param>
    /// <returns>A dictionary mapping process keys to process IDs</returns>
    Task<Dictionary<string, int>> BatchGetOrCreateProcessesAsync(List<(string processName, string? processPath)> processes);
    
    /// <summary>
    /// Creates multiple process snapshots in a batch
    /// </summary>
    /// <param name="snapshotId">The snapshot ID</param>
    /// <param name="processSnapshots">The list of process snapshots to create</param>
    Task BatchCreateProcessSnapshotsAsync(long snapshotId, List<(int processId, ProcessInfo processInfo)> processSnapshots);
    
    /// <summary>
    /// Creates a complete snapshot with all associated data in a single transaction
    /// </summary>
    /// <param name="totalCpuUsage">The total CPU usage percentage</param>
    /// <param name="totalMemoryMb">The total memory usage in megabytes</param>
    /// <param name="availableMemoryMb">The available memory in megabytes</param>
    /// <param name="processSnapshots">The list of process snapshots</param>
    /// <param name="cpuTemperature">The CPU temperature data</param>
    /// <returns>The ID of the created snapshot</returns>
    Task<long> CreateSnapshotWithDataAsync(
        decimal? totalCpuUsage, 
        long? totalMemoryMb, 
        long? availableMemoryMb,
        List<(int processId, ProcessInfo processInfo)> processSnapshots,
        CpuTemperature? cpuTemperature);
    
    /// <summary>
    /// Checks if the database connection is currently available
    /// </summary>
    /// <returns>True if the connection is available, false otherwise</returns>
    Task<bool> IsConnectionAvailableAsync();
    
    /// <summary>
    /// Restores an offline snapshot to the database
    /// </summary>
    /// <param name="snapshotData">The offline snapshot data</param>
    /// <returns>The ID of the restored snapshot</returns>
    Task<long> RestoreOfflineSnapshotAsync(OfflineSnapshotData snapshotData);
    
    /// <summary>
    /// Restores an offline process record to the database
    /// </summary>
    /// <param name="processData">The offline process data</param>
    /// <returns>The ID of the restored process</returns>
    Task<int> RestoreOfflineProcessAsync(OfflineProcessData processData);
    
    /// <summary>
    /// Restores a complete offline snapshot batch to the database
    /// </summary>
    /// <param name="batch">The offline snapshot batch</param>
    Task RestoreOfflineSnapshotBatchAsync(OfflineSnapshotBatch batch);
}