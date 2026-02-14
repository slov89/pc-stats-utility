namespace PCStats.Data;

/// <summary>
/// Represents summary information about a system snapshot
/// </summary>
public class SnapshotInfo
{
    /// <summary>
    /// Gets or sets the unique identifier for this snapshot
    /// </summary>
    public long SnapshotId { get; set; }
    
    /// <summary>
    /// Gets or sets the timestamp when this snapshot was captured
    /// </summary>
    public DateTime SnapshotTimestamp { get; set; }
    
    /// <summary>
    /// Gets or sets the total CPU usage percentage across all cores
    /// </summary>
    public decimal? TotalCpuUsage { get; set; }
    
    /// <summary>
    /// Gets or sets the total memory usage in megabytes
    /// </summary>
    public long? TotalMemoryUsageMb { get; set; }
    
    /// <summary>
    /// Gets or sets the available memory in megabytes
    /// </summary>
    public long? TotalAvailableMemoryMb { get; set; }
}
