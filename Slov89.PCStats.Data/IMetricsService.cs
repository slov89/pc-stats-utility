using Slov89.PCStats.Models;

namespace Slov89.PCStats.Data;

/// <summary>
/// Provides methods for querying system metrics and performance data from the database
/// </summary>
public interface IMetricsService
{
    /// <summary>
    /// Gets all system snapshots within the specified time range
    /// </summary>
    /// <param name="startTime">The start of the time range</param>
    /// <param name="endTime">The end of the time range</param>
    /// <returns>A list of snapshots ordered by timestamp</returns>
    Task<List<Snapshot>> GetSnapshotsAsync(DateTime startTime, DateTime endTime);
    
    /// <summary>
    /// Gets all CPU temperature readings within the specified time range
    /// </summary>
    /// <param name="startTime">The start of the time range</param>
    /// <param name="endTime">The end of the time range</param>
    /// <returns>A list of CPU temperature readings ordered by timestamp</returns>
    Task<List<CpuTemperature>> GetCpuTemperaturesAsync(DateTime startTime, DateTime endTime);
    
    /// <summary>
    /// Gets the top processes by resource usage within the specified time range
    /// </summary>
    /// <param name="startTime">The start of the time range</param>
    /// <param name="endTime">The end of the time range</param>
    /// <param name="topCount">The number of top processes to return (default is 5)</param>
    /// <returns>A dictionary mapping process names to their time-series metrics</returns>
    Task<Dictionary<string, List<(DateTime timestamp, decimal cpuUsage, long memoryMb)>>> GetTopProcessesAsync(DateTime startTime, DateTime endTime, int topCount = 5);
    
    /// <summary>
    /// Gets process snapshots grouped by snapshot ID within the specified time range
    /// </summary>
    /// <param name="startTime">The start of the time range</param>
    /// <param name="endTime">The end of the time range</param>
    /// <returns>A dictionary mapping snapshot IDs to lists of process snapshots</returns>
    Task<Dictionary<long, List<ProcessSnapshotWithName>>> GetProcessSnapshotsAsync(DateTime startTime, DateTime endTime);
    
    /// <summary>
    /// Gets the latest process snapshots from the most recent system snapshot
    /// </summary>
    /// <returns>A list of detailed process snapshots</returns>
    Task<List<ProcessSnapshotDetail>> GetLatestProcessSnapshotsAsync();
    
    /// <summary>
    /// Gets summary information for all snapshots in the database
    /// </summary>
    /// <returns>A list of snapshot information records</returns>
    Task<List<SnapshotInfo>> GetAllSnapshotInfosAsync();
    
    /// <summary>
    /// Gets summary information for the most recent snapshot
    /// </summary>
    /// <returns>The latest snapshot information, or null if no snapshots exist</returns>
    Task<SnapshotInfo?> GetLatestSnapshotInfoAsync();
    
    /// <summary>
    /// Gets summary information for a specific snapshot
    /// </summary>
    /// <param name="snapshotId">The ID of the snapshot to retrieve</param>
    /// <returns>The snapshot information, or null if not found</returns>
    Task<SnapshotInfo?> GetSnapshotInfoAsync(long snapshotId);
    
    /// <summary>
    /// Gets all process snapshots associated with a specific system snapshot
    /// </summary>
    /// <param name="snapshotId">The ID of the system snapshot</param>
    /// <returns>A list of detailed process snapshots</returns>
    Task<List<ProcessSnapshotDetail>> GetProcessSnapshotsByIdAsync(long snapshotId);
}