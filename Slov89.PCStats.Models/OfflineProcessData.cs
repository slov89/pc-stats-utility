using System.Text.Json.Serialization;

namespace Slov89.PCStats.Models;

/// <summary>
/// Data structure for offline GetOrCreateProcess operations
/// </summary>
public class OfflineProcessData
{
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
    /// Gets or sets the local process ID used for offline correlation
    /// </summary>
    [JsonPropertyName("local_process_id")]
    public int LocalProcessId { get; set; }
}
