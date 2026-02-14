namespace Slov89.PCStats.Data;

/// <summary>
/// Represents detailed information about a process snapshot including process metadata
/// </summary>
public class ProcessSnapshotDetail
{
    /// <summary>
    /// Gets or sets the unique identifier for this process snapshot
    /// </summary>
    public long ProcessSnapshotId { get; set; }
    
    /// <summary>
    /// Gets or sets the process ID (PID) at the time of the snapshot
    /// </summary>
    public int Pid { get; set; }
    
    /// <summary>
    /// Gets or sets the name of the process
    /// </summary>
    public string ProcessName { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the file path of the process executable
    /// </summary>
    public string? ProcessPath { get; set; }
    
    /// <summary>
    /// Gets or sets the CPU usage percentage
    /// </summary>
    public decimal? CpuUsage { get; set; }
    
    /// <summary>
    /// Gets or sets the private memory usage in megabytes
    /// </summary>
    public long? PrivateMemoryMb { get; set; }
    
    /// <summary>
    /// Gets or sets the VRAM (video memory) usage in megabytes
    /// </summary>
    public long? VramUsageMb { get; set; }
    
    /// <summary>
    /// Gets or sets the number of threads in the process
    /// </summary>
    public int? ThreadCount { get; set; }
    
    /// <summary>
    /// Gets or sets the number of handles held by the process
    /// </summary>
    public int? HandleCount { get; set; }
    
    /// <summary>
    /// Gets or sets the timestamp when this process was first seen
    /// </summary>
    public DateTime FirstSeen { get; set; }
    
    /// <summary>
    /// Gets or sets the timestamp when this process was last seen
    /// </summary>
    public DateTime LastSeen { get; set; }
}
