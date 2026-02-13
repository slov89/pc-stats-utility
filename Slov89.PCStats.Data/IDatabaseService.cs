using Slov89.PCStats.Models;

namespace Slov89.PCStats.Data;

public interface IDatabaseService
{
    Task InitializeAsync();
    Task<long> CreateSnapshotAsync(decimal? totalCpuUsage, long? totalMemoryMb, long? availableMemoryMb);
    Task<int> GetOrCreateProcessAsync(string processName, string? processPath);
    Task CreateProcessSnapshotAsync(long snapshotId, int processId, ProcessInfo processInfo);
    Task CreateCpuTemperatureAsync(long snapshotId, CpuTemperature temperature);
    Task TestConnectionAsync();
    Task<int> CleanupOldSnapshotsAsync(int daysToKeep);
    
    // Offline storage recovery methods
    Task<bool> IsConnectionAvailableAsync();
    Task<long> RestoreOfflineSnapshotAsync(OfflineSnapshotData snapshotData);
    Task<int> RestoreOfflineProcessAsync(OfflineProcessData processData);
    Task RestoreOfflineSnapshotBatchAsync(OfflineSnapshotBatch batch);
}
