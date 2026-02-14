namespace PCStats.Models;

/// <summary>
/// Represents a snapshot of a specific process's resource usage at a point in time
/// </summary>
public class ProcessSnapshot
{
    /// <summary>
    /// Gets or sets the unique identifier for this process snapshot
    /// </summary>
    public long ProcessSnapshotId { get; set; }
    
    /// <summary>
    /// Gets or sets the snapshot ID this process snapshot belongs to
    /// </summary>
    public long SnapshotId { get; set; }
    
    /// <summary>
    /// Gets or sets the process ID from the processes table
    /// </summary>
    public int ProcessId { get; set; }
    
    /// <summary>
    /// Gets or sets the operating system process ID (PID)
    /// </summary>
    public int Pid { get; set; }
    
    /// <summary>
    /// Gets or sets the CPU usage percentage
    /// </summary>
    public decimal? CpuUsage { get; set; }
    
    /// <summary>
    /// Gets or sets the total memory usage in megabytes
    /// </summary>
    public long? MemoryUsageMb { get; set; }
    
    /// <summary>
    /// Gets or sets the private memory usage in megabytes
    /// </summary>
    public long? PrivateMemoryMb { get; set; }
    
    /// <summary>
    /// Gets or sets the virtual memory usage in megabytes
    /// </summary>
    public long? VirtualMemoryMb { get; set; }
    
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
}
