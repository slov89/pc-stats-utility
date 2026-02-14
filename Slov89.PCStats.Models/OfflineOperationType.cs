namespace Slov89.PCStats.Models;

/// <summary>
/// Defines the types of operations that can be queued for offline storage
/// </summary>
public enum OfflineOperationType
{
    /// <summary>
    /// Operation to create a system snapshot
    /// </summary>
    CreateSnapshot,
    
    /// <summary>
    /// Operation to get or create a process record
    /// </summary>
    GetOrCreateProcess,
    
    /// <summary>
    /// Operation to create a process snapshot
    /// </summary>
    CreateProcessSnapshot,
    
    /// <summary>
    /// Operation to record CPU temperature
    /// </summary>
    CreateCpuTemperature,
    
    /// <summary>
    /// Operation to create multiple process snapshots in a batch
    /// </summary>
    BatchCreateProcessSnapshots
}
