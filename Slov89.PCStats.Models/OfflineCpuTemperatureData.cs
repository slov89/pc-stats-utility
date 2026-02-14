using System.Text.Json.Serialization;

namespace Slov89.PCStats.Models;

/// <summary>
/// Data structure for offline CreateCpuTemperature operations
/// </summary>
public class OfflineCpuTemperatureData
{
    /// <summary>
    /// Gets or sets the local snapshot ID this temperature reading belongs to
    /// </summary>
    [JsonPropertyName("local_snapshot_id")]
    public long LocalSnapshotId { get; set; }
    
    /// <summary>
    /// Gets or sets the CPU temperature measurements
    /// </summary>
    [JsonPropertyName("temperature")]
    public CpuTemperature Temperature { get; set; } = new();
}
