using Microsoft.Extensions.Logging;
using Slov89.PCStats.Data;
using Slov89.PCStats.Models;

namespace Slov89.PCStats.Service.Services;

/// <summary>
/// Wrapper around DatabaseService that provides offline storage capabilities
/// when database connection is unavailable
/// </summary>
public class OfflineDatabaseService : IDatabaseService
{
    private readonly IDatabaseService _databaseService;
    private readonly IOfflineStorageService _offlineStorage;
    private readonly ILogger<OfflineDatabaseService> _logger;
    private bool _isOfflineMode = false;

    public OfflineDatabaseService(
        IDatabaseService databaseService,
        IOfflineStorageService offlineStorage,
        ILogger<OfflineDatabaseService> logger)
    {
        _databaseService = databaseService;
        _offlineStorage = offlineStorage;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        try
        {
            await _databaseService.InitializeAsync();
            _logger.LogInformation("Database connection established - operating in online mode");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Database connection failed during initialization - starting in offline mode");
            _isOfflineMode = true;
        }
    }

    public async Task<bool> IsConnectionAvailableAsync()
    {
        return await _databaseService.IsConnectionAvailableAsync();
    }

    public async Task TestConnectionAsync()
    {
        await _databaseService.TestConnectionAsync();
    }

    public async Task<long> CreateSnapshotAsync(decimal? totalCpuUsage, long? totalMemoryMb, long? availableMemoryMb)
    {
        try
        {
            var result = await _databaseService.CreateSnapshotAsync(totalCpuUsage, totalMemoryMb, availableMemoryMb);
            
            // If we were in offline mode and this succeeds, we're back online
            if (_isOfflineMode)
            {
                _isOfflineMode = false;
                _logger.LogInformation("Database connection restored, switching back to online mode");
                
                // Start recovery process in background
                _ = Task.Run(async () => await TryRecoverOfflineDataAsync());
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Database unavailable for snapshot creation, switching to offline mode");
            _isOfflineMode = true;
            
            // Return a local snapshot ID for offline storage
            var localSnapshotId = _offlineStorage.GetNextLocalSnapshotId();
            
            // Store the snapshot data for later recovery
            var snapshotData = new OfflineSnapshotData
            {
                TotalCpuUsage = totalCpuUsage,
                TotalMemoryMb = totalMemoryMb,
                AvailableMemoryMb = availableMemoryMb,
                LocalSnapshotId = localSnapshotId,
                Timestamp = DateTime.UtcNow
            };

            // Create a batch for this snapshot (will be populated by other calls)
            var batch = new OfflineSnapshotBatch
            {
                LocalSnapshotId = localSnapshotId,
                SnapshotData = snapshotData
            };
            
            await _offlineStorage.SaveOfflineSnapshotAsync(batch);
            
            return localSnapshotId;
        }
    }

    public async Task<int> GetOrCreateProcessAsync(string processName, string? processPath)
    {
        if (_isOfflineMode)
        {
            // In offline mode, return a deterministic hash-based ID
            var processKey = $"{processName}|{processPath}";
            return Math.Abs(processKey.GetHashCode());
        }

        try
        {
            return await _databaseService.GetOrCreateProcessAsync(processName, processPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Database unavailable for process creation, switching to offline mode");
            _isOfflineMode = true;
            
            // Return a hash-based ID for offline storage
            var processKey = $"{processName}|{processPath}";
            return Math.Abs(processKey.GetHashCode());
        }
    }

    public async Task CreateProcessSnapshotAsync(long snapshotId, int processId, ProcessInfo processInfo)
    {
        if (_isOfflineMode)
        {
            await AppendToOfflineBatch(snapshotId, processId, processInfo);
            return;
        }

        try
        {
            await _databaseService.CreateProcessSnapshotAsync(snapshotId, processId, processInfo);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Database unavailable for process snapshot creation, switching to offline mode");
            _isOfflineMode = true;
            await AppendToOfflineBatch(snapshotId, processId, processInfo);
        }
    }

    public async Task CreateCpuTemperatureAsync(long snapshotId, CpuTemperature temperature)
    {
        if (_isOfflineMode)
        {
            await AppendCpuTemperatureToOfflineBatch(snapshotId, temperature);
            return;
        }

        try
        {
            await _databaseService.CreateCpuTemperatureAsync(snapshotId, temperature);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Database unavailable for CPU temperature creation, switching to offline mode");
            _isOfflineMode = true;
            await AppendCpuTemperatureToOfflineBatch(snapshotId, temperature);
        }
    }

    private async Task AppendToOfflineBatch(long localSnapshotId, int processId, ProcessInfo processInfo)
    {
        // Try to find existing batch or create a minimal one
        var batches = await _offlineStorage.GetPendingOfflineSnapshotsAsync();
        var batch = batches.FirstOrDefault(b => b.LocalSnapshotId == localSnapshotId);
        
        if (batch == null)
        {
            // Create a minimal batch if snapshot wasn't created offline
            batch = new OfflineSnapshotBatch
            {
                LocalSnapshotId = localSnapshotId
            };
        }

        // Add the process snapshot
        batch.ProcessSnapshots.Add(new OfflineProcessSnapshotData
        {
            LocalSnapshotId = localSnapshotId,
            LocalProcessId = processId,
            ProcessInfo = processInfo,
            ProcessName = processInfo.ProcessName,
            ProcessPath = processInfo.ProcessPath
        });

        await _offlineStorage.SaveOfflineSnapshotAsync(batch);
    }

    private async Task AppendCpuTemperatureToOfflineBatch(long localSnapshotId, CpuTemperature temperature)
    {
        // Try to find existing batch
        var batches = await _offlineStorage.GetPendingOfflineSnapshotsAsync();
        var batch = batches.FirstOrDefault(b => b.LocalSnapshotId == localSnapshotId);
        
        if (batch == null)
        {
            // Create a minimal batch if snapshot wasn't created offline
            batch = new OfflineSnapshotBatch
            {
                LocalSnapshotId = localSnapshotId
            };
        }

        // Add CPU temperature data
        batch.CpuTemperature = new OfflineCpuTemperatureData
        {
            LocalSnapshotId = localSnapshotId,
            Temperature = temperature
        };

        await _offlineStorage.SaveOfflineSnapshotAsync(batch);
    }

    private async Task TryRecoverOfflineDataAsync()
    {
        try
        {
            _logger.LogInformation("Starting offline data recovery...");
            
            var pendingBatches = await _offlineStorage.GetPendingOfflineSnapshotsAsync();
            if (!pendingBatches.Any())
            {
                _logger.LogInformation("No offline data to recover");
                return;
            }

            _logger.LogInformation("Found {Count} offline snapshot batches to recover", pendingBatches.Count);
            
            int successCount = 0;
            int failCount = 0;

            foreach (var batch in pendingBatches.OrderBy(b => b.Timestamp))
            {
                try
                {
                    if (!await _databaseService.IsConnectionAvailableAsync())
                    {
                        _logger.LogWarning("Database connection lost during recovery, stopping");
                        _isOfflineMode = true;
                        break;
                    }

                    await _databaseService.RestoreOfflineSnapshotBatchAsync(batch);
                    await _offlineStorage.RemoveOfflineSnapshotAsync(batch.BatchId);
                    
                    successCount++;
                    
                    if (successCount % 10 == 0)
                    {
                        _logger.LogInformation("Recovered {SuccessCount} of {TotalCount} offline snapshot batches", 
                            successCount, pendingBatches.Count);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to recover offline snapshot batch {BatchId}", batch.BatchId);
                    failCount++;
                    
                    // Increment retry count
                    batch.RetryCount++;
                    batch.ErrorMessage = ex.Message;
                    
                    // Give up after 3 retries
                    if (batch.RetryCount >= 3)
                    {
                        _logger.LogWarning("Giving up on offline snapshot batch {BatchId} after 3 retries", batch.BatchId);
                        await _offlineStorage.RemoveOfflineSnapshotAsync(batch.BatchId);
                    }
                    else
                    {
                        await _offlineStorage.SaveOfflineSnapshotAsync(batch);
                    }
                }
            }

            _logger.LogInformation("Offline data recovery completed: {SuccessCount} succeeded, {FailCount} failed", 
                successCount, failCount);
            
            // Clean up old files
            await _offlineStorage.CleanupOldOfflineDataAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Offline data recovery failed");
        }
    }

    public async Task<long> RestoreOfflineSnapshotAsync(OfflineSnapshotData snapshotData)
    {
        return await _databaseService.RestoreOfflineSnapshotAsync(snapshotData);
    }

    public async Task<int> RestoreOfflineProcessAsync(OfflineProcessData processData)
    {
        return await _databaseService.RestoreOfflineProcessAsync(processData);
    }

    public async Task RestoreOfflineSnapshotBatchAsync(OfflineSnapshotBatch batch)
    {
        await _databaseService.RestoreOfflineSnapshotBatchAsync(batch);
    }

    public async Task<int> CleanupOldSnapshotsAsync(int daysToKeep)
    {
        // Cleanup doesn't need offline mode support - always call database directly
        return await _databaseService.CleanupOldSnapshotsAsync(daysToKeep);
    }

    public async Task<Dictionary<string, int>> BatchGetOrCreateProcessesAsync(List<(string processName, string? processPath)> processes)
    {
        if (_isOfflineMode)
        {
            // In offline mode, return deterministic hash-based IDs
            var result = new Dictionary<string, int>();
            foreach (var (processName, processPath) in processes)
            {
                var processKey = $"{processName}|{processPath ?? ""}";
                result[processKey] = Math.Abs(processKey.GetHashCode());
            }
            return result;
        }

        try
        {
            return await _databaseService.BatchGetOrCreateProcessesAsync(processes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Database unavailable for batch process creation, switching to offline mode");
            _isOfflineMode = true;
            
            // Return hash-based IDs for offline storage
            var result = new Dictionary<string, int>();
            foreach (var (processName, processPath) in processes)
            {
                var processKey = $"{processName}|{processPath ?? ""}";
                result[processKey] = Math.Abs(processKey.GetHashCode());
            }
            return result;
        }
    }

    public async Task BatchCreateProcessSnapshotsAsync(long snapshotId, List<(int processId, ProcessInfo processInfo)> processSnapshots)
    {
        if (_isOfflineMode)
        {
            // In offline mode, append each to the batch
            foreach (var (processId, processInfo) in processSnapshots)
            {
                await AppendToOfflineBatch(snapshotId, processId, processInfo);
            }
            return;
        }

        try
        {
            await _databaseService.BatchCreateProcessSnapshotsAsync(snapshotId, processSnapshots);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Database unavailable for batch process snapshot creation, switching to offline mode");
            _isOfflineMode = true;
            
            // Append each to offline batch
            foreach (var (processId, processInfo) in processSnapshots)
            {
                await AppendToOfflineBatch(snapshotId, processId, processInfo);
            }
        }
    }

    public async Task<long> CreateSnapshotWithDataAsync(
        decimal? totalCpuUsage, 
        long? totalMemoryMb, 
        long? availableMemoryMb,
        List<(int processId, ProcessInfo processInfo)> processSnapshots,
        CpuTemperature? cpuTemperature)
    {
        try
        {
            var result = await _databaseService.CreateSnapshotWithDataAsync(
                totalCpuUsage, 
                totalMemoryMb, 
                availableMemoryMb,
                processSnapshots,
                cpuTemperature);
            
            // If we were in offline mode and this succeeds, we're back online
            if (_isOfflineMode)
            {
                _isOfflineMode = false;
                _logger.LogInformation("Database connection restored, switching back to online mode");
                
                // Start recovery process in background
                _ = Task.Run(async () => await TryRecoverOfflineDataAsync());
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Database unavailable for snapshot creation, switching to offline mode");
            _isOfflineMode = true;
            
            // Return a local snapshot ID for offline storage
            var localSnapshotId = _offlineStorage.GetNextLocalSnapshotId();
            
            // Store the snapshot data for later recovery
            var snapshotData = new OfflineSnapshotData
            {
                TotalCpuUsage = totalCpuUsage,
                TotalMemoryMb = totalMemoryMb,
                AvailableMemoryMb = availableMemoryMb,
                LocalSnapshotId = localSnapshotId,
                Timestamp = DateTime.UtcNow
            };

            // Create a batch for this snapshot
            var batch = new OfflineSnapshotBatch
            {
                LocalSnapshotId = localSnapshotId,
                SnapshotData = snapshotData
            };

            // Add process snapshots
            foreach (var (processId, processInfo) in processSnapshots)
            {
                batch.ProcessSnapshots.Add(new OfflineProcessSnapshotData
                {
                    LocalSnapshotId = localSnapshotId,
                    LocalProcessId = processId,
                    ProcessInfo = processInfo,
                    ProcessName = processInfo.ProcessName,
                    ProcessPath = processInfo.ProcessPath
                });
            }

            // Add CPU temperature if provided
            if (cpuTemperature != null)
            {
                batch.CpuTemperature = new OfflineCpuTemperatureData
                {
                    LocalSnapshotId = localSnapshotId,
                    Temperature = cpuTemperature
                };
            }
            
            await _offlineStorage.SaveOfflineSnapshotAsync(batch);
            
            return localSnapshotId;
        }
    }
}
