namespace PCStats.Models;

/// <summary>
/// Represents a process that has been monitored by the system
/// </summary>
public class Process
{
    /// <summary>
    /// Gets or sets the unique identifier for this process record
    /// </summary>
    public int ProcessId { get; set; }
    
    /// <summary>
    /// Gets or sets the name of the process
    /// </summary>
    public string ProcessName { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the file path of the process executable
    /// </summary>
    public string? ProcessPath { get; set; }
    
    /// <summary>
    /// Gets or sets the timestamp when this process was first observed
    /// </summary>
    public DateTime FirstSeen { get; set; }
    
    /// <summary>
    /// Gets or sets the timestamp when this process was last observed
    /// </summary>
    public DateTime LastSeen { get; set; }
}
