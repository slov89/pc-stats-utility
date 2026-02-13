using System.Text.Json.Serialization;

namespace Slov89.PCStats.Models;

/// <summary>
/// Data structure for offline CreateSnapshot operations
/// </summary>
public class OfflineSnapshotData
{
    [JsonPropertyName("total_cpu_usage")]
    public decimal? TotalCpuUsage { get; set; }
    
    [JsonPropertyName("total_memory_mb")]
    public long? TotalMemoryMb { get; set; }
    
    [JsonPropertyName("available_memory_mb")]
    public long? AvailableMemoryMb { get; set; }
    
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    [JsonPropertyName("local_snapshot_id")]
    public long LocalSnapshotId { get; set; }
}

/// <summary>
/// Data structure for offline GetOrCreateProcess operations
/// </summary>
public class OfflineProcessData
{
    [JsonPropertyName("process_name")]
    public string ProcessName { get; set; } = string.Empty;
    
    [JsonPropertyName("process_path")]
    public string? ProcessPath { get; set; }
    
    [JsonPropertyName("local_process_id")]
    public int LocalProcessId { get; set; }
}

/// <summary>
/// Data structure for offline CreateProcessSnapshot operations
/// </summary>
public class OfflineProcessSnapshotData
{
    [JsonPropertyName("local_snapshot_id")]
    public long LocalSnapshotId { get; set; }
    
    [JsonPropertyName("local_process_id")]
    public int LocalProcessId { get; set; }
    
    [JsonPropertyName("process_name")]
    public string ProcessName { get; set; } = string.Empty;
    
    [JsonPropertyName("process_path")]
    public string? ProcessPath { get; set; }
    
    [JsonPropertyName("process_info")]
    public ProcessInfo ProcessInfo { get; set; } = new();
}

/// <summary>
/// Data structure for offline CreateCpuTemperature operations
/// </summary>
public class OfflineCpuTemperatureData
{
    [JsonPropertyName("local_snapshot_id")]
    public long LocalSnapshotId { get; set; }
    
    [JsonPropertyName("temperature")]
    public CpuTemperature Temperature { get; set; } = new();
}

/// <summary>
/// Data structure for offline BatchCreateProcessSnapshots operations
/// </summary>
public class OfflineBatchProcessSnapshotsData
{
    [JsonPropertyName("local_snapshot_id")]
    public long LocalSnapshotId { get; set; }
    
    [JsonPropertyName("process_snapshots")]
    public List<OfflineProcessSnapshotData> ProcessSnapshots { get; set; } = new();
}

/// <summary>
/// Container for all offline operations in a single snapshot cycle
/// </summary>
public class OfflineSnapshotBatch
{
    [JsonPropertyName("batch_id")]
    public Guid BatchId { get; set; } = Guid.NewGuid();
    
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    [JsonPropertyName("local_snapshot_id")]
    public long LocalSnapshotId { get; set; }
    
    [JsonPropertyName("snapshot_data")]
    public OfflineSnapshotData? SnapshotData { get; set; }
    
    [JsonPropertyName("process_snapshots")]
    public List<OfflineProcessSnapshotData> ProcessSnapshots { get; set; } = new();
    
    [JsonPropertyName("cpu_temperature")]
    public OfflineCpuTemperatureData? CpuTemperature { get; set; }
    
    [JsonPropertyName("retry_count")]
    public int RetryCount { get; set; } = 0;
    
    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }
}
