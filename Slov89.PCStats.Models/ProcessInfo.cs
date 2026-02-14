namespace Slov89.PCStats.Models;

/// <summary>
/// Represents detailed runtime information about a running process
/// </summary>
public class ProcessInfo
{
    /// <summary>
    /// Gets or sets the operating system process ID (PID)
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
    public decimal CpuUsage { get; set; }
    
    /// <summary>
    /// Gets or sets the total memory usage in megabytes
    /// </summary>
    public long MemoryUsageMb { get; set; }
    
    /// <summary>
    /// Gets or sets the private memory usage in megabytes
    /// </summary>
    public long PrivateMemoryMb { get; set; }
    
    /// <summary>
    /// Gets or sets the virtual memory usage in megabytes
    /// </summary>
    public long VirtualMemoryMb { get; set; }
    
    /// <summary>
    /// Gets or sets the VRAM (video memory) usage in megabytes
    /// </summary>
    public long VramUsageMb { get; set; }
    
    /// <summary>
    /// Gets or sets the number of threads in the process
    /// </summary>
    public int ThreadCount { get; set; }
    
    /// <summary>
    /// Gets or sets the number of handles held by the process
    /// </summary>
    public int HandleCount { get; set; }
}
