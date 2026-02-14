using System.Text.Json.Serialization;

namespace PCStats.Models;

/// <summary>
/// Data structure for offline CreateSnapshot operations
/// </summary>
public class OfflineSnapshotData
{
    /// <summary>
    /// Gets or sets the total CPU usage percentage across all cores
    /// </summary>
    [JsonPropertyName("total_cpu_usage")]
    public decimal? TotalCpuUsage { get; set; }
    
    /// <summary>
    /// Gets or sets the total memory usage in megabytes
    /// </summary>
    [JsonPropertyName("total_memory_mb")]
    public long? TotalMemoryMb { get; set; }
    
    /// <summary>
    /// Gets or sets the available memory in megabytes
    /// </summary>
    [JsonPropertyName("available_memory_mb")]
    public long? AvailableMemoryMb { get; set; }
    
    /// <summary>
    /// Gets or sets the timestamp when this snapshot was captured
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Gets or sets the local snapshot ID used for offline correlation
    /// </summary>
    [JsonPropertyName("local_snapshot_id")]
    public long LocalSnapshotId { get; set; }
}
