using System.Text.Json.Serialization;

namespace PCStats.Models;

/// <summary>
/// Data structure for offline CreateProcessSnapshot operations
/// </summary>
public class OfflineProcessSnapshotData
{
    /// <summary>
    /// Gets or sets the local snapshot ID this process snapshot belongs to
    /// </summary>
    [JsonPropertyName("local_snapshot_id")]
    public long LocalSnapshotId { get; set; }
    
    /// <summary>
    /// Gets or sets the local process ID for this snapshot
    /// </summary>
    [JsonPropertyName("local_process_id")]
    public int LocalProcessId { get; set; }
    
    /// <summary>
    /// Gets or sets the name of the process
    /// </summary>
    [JsonPropertyName("process_name")]
    public string ProcessName { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the file path of the process executable
    /// </summary>
    [JsonPropertyName("process_path")]
    public string? ProcessPath { get; set; }
    
    /// <summary>
    /// Gets or sets the detailed process information including resource usage
    /// </summary>
    [JsonPropertyName("process_info")]
    public ProcessInfo ProcessInfo { get; set; } = new();
}
