namespace PCStats.Data;

/// <summary>
/// Represents an aggregated view of process snapshots grouped by process name
/// </summary>
public class ProcessSnapshotWithName
{
    /// <summary>
    /// Gets or sets the snapshot ID
    /// </summary>
    public long SnapshotId { get; set; }
    
    /// <summary>
    /// Gets or sets the name of the process
    /// </summary>
    public string ProcessName { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the private memory usage in megabytes
    /// </summary>
    public long? PrivateMemoryMb { get; set; }
    
    /// <summary>
    /// Gets or sets the total memory usage in megabytes
    /// </summary>
    public long? MemoryUsageMb { get; set; }
    
    /// <summary>
    /// Gets or sets the CPU usage percentage
    /// </summary>
    public decimal? CpuUsage { get; set; }
    
    /// <summary>
    /// Gets or sets the count of processes with this name
    /// </summary>
    public int ProcessCount { get; set; }
}
