using System.Text.Json.Serialization;

namespace PCStats.Models;

/// <summary>
/// Represents a data operation that failed to save to database and is stored offline
/// </summary>
public class OfflineOperation
{
    /// <summary>
    /// Gets or sets the unique identifier for this operation
    /// </summary>
    [JsonPropertyName("operation_id")]
    public Guid OperationId { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Gets or sets the timestamp when this operation was created
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Gets or sets the type of offline operation
    /// </summary>
    [JsonPropertyName("operation_type")]
    public OfflineOperationType OperationType { get; set; }
    
    /// <summary>
    /// Gets or sets the operation data object
    /// </summary>
    [JsonPropertyName("data")]
    public object Data { get; set; } = null!;
    
    /// <summary>
    /// Gets or sets the number of times this operation has been retried
    /// </summary>
    [JsonPropertyName("retry_count")]
    public int RetryCount { get; set; } = 0;
    
    /// <summary>
    /// Gets or sets the error message from the last failed attempt
    /// </summary>
    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }
}