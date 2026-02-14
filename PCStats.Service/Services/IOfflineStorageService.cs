using PCStats.Models;

namespace PCStats.Service.Services;

public interface IOfflineStorageService
{
    /// <summary>
    /// Saves a snapshot batch to offline storage when database is unavailable
    /// </summary>
    Task SaveOfflineSnapshotAsync(OfflineSnapshotBatch batch);
    
    /// <summary>
    /// Gets all offline snapshots that need to be restored to the database
    /// </summary>
    Task<List<OfflineSnapshotBatch>> GetPendingOfflineSnapshotsAsync();
    
    /// <summary>
    /// Removes successfully restored snapshots from offline storage
    /// </summary>
    Task RemoveOfflineSnapshotAsync(Guid batchId);
    
    /// <summary>
    /// Checks if database connection is available
    /// </summary>
    Task<bool> IsRecoveryNeededAsync();
    
    /// <summary>
    /// Gets the count of pending offline snapshots
    /// </summary>
    Task<int> GetPendingSnapshotCountAsync();
    
    /// <summary>
    /// Cleans up old offline files (configurable retention)
    /// </summary>
    Task CleanupOldOfflineDataAsync();
    
    /// <summary>
    /// Gets the next local snapshot ID for offline storage
    /// </summary>
    long GetNextLocalSnapshotId();
}