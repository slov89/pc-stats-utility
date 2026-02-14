using System.Text.Json.Serialization;

namespace PCStats.Models;

/// <summary>
/// Data structure for offline BatchCreateProcessSnapshots operations
/// </summary>
public class OfflineBatchProcessSnapshotsData
{
    /// <summary>
    /// Gets or sets the local snapshot ID for this batch of process snapshots
    /// </summary>
    [JsonPropertyName("local_snapshot_id")]
    public long LocalSnapshotId { get; set; }
    
    /// <summary>
    /// Gets or sets the collection of process snapshots in this batch
    /// </summary>
    [JsonPropertyName("process_snapshots")]
    public List<OfflineProcessSnapshotData> ProcessSnapshots { get; set; } = new();
}
