using System.Text.Json.Serialization;

namespace PCStats.Models;

/// <summary>
/// Container for all offline operations in a single snapshot cycle
/// </summary>
public class OfflineSnapshotBatch
{
    /// <summary>
    /// Gets or sets the unique identifier for this batch
    /// </summary>
    [JsonPropertyName("batch_id")]
    public Guid BatchId { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Gets or sets the timestamp when this batch was created
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Gets or sets the local snapshot ID for this batch
    /// </summary>
    [JsonPropertyName("local_snapshot_id")]
    public long LocalSnapshotId { get; set; }
    
    /// <summary>
    /// Gets or sets the snapshot data for system-level metrics
    /// </summary>
    [JsonPropertyName("snapshot_data")]
    public OfflineSnapshotData? SnapshotData { get; set; }
    
    /// <summary>
    /// Gets or sets the collection of process-level snapshots
    /// </summary>
    [JsonPropertyName("process_snapshots")]
    public List<OfflineProcessSnapshotData> ProcessSnapshots { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the CPU temperature data for this snapshot
    /// </summary>
    [JsonPropertyName("cpu_temperature")]
    public OfflineCpuTemperatureData? CpuTemperature { get; set; }
    
    /// <summary>
    /// Gets or sets the number of times this batch has been retried
    /// </summary>
    [JsonPropertyName("retry_count")]
    public int RetryCount { get; set; } = 0;
    
    /// <summary>
    /// Gets or sets the error message from the last failed attempt
    /// </summary>
    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }
}
