using System.Text.Json.Serialization;

namespace Slov89.PCStats.Models;

/// <summary>
/// Represents a data operations that failed to save to database and is stored offline
/// </summary>
public class OfflineOperation
{
    [JsonPropertyName("operation_id")]
    public Guid OperationId { get; set; } = Guid.NewGuid();
    
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    [JsonPropertyName("operation_type")]
    public OfflineOperationType OperationType { get; set; }
    
    [JsonPropertyName("data")]
    public object Data { get; set; } = null!;
    
    [JsonPropertyName("retry_count")]
    public int RetryCount { get; set; } = 0;
    
    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }
}

public enum OfflineOperationType
{
    CreateSnapshot,
    GetOrCreateProcess,
    CreateProcessSnapshot,
    CreateCpuTemperature,
    BatchCreateProcessSnapshots
}