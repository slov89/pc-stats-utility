using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Slov89.PCStats.Data;
using Slov89.PCStats.Models;
using System.Text.Json;

namespace Slov89.PCStats.Service.Services;

/// <summary>
/// Manages offline storage of snapshot data when database connectivity is unavailable
/// </summary>
public class OfflineStorageService : IOfflineStorageService
{
    private readonly ILogger<OfflineStorageService> _logger;
    private readonly string _offlineStoragePath;
    private readonly int _maxRetentionDays;
    private readonly JsonSerializerOptions _jsonOptions;
    private long _nextLocalSnapshotId = 1;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    public OfflineStorageService(
        ILogger<OfflineStorageService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        
        _offlineStoragePath = configuration.GetValue<string>("OfflineStorage:Path") 
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), 
                           "Slov89.PCStats.Service", "OfflineData");
        
        _maxRetentionDays = configuration.GetValue<int>("OfflineStorage:MaxRetentionDays", 7);
        
        Directory.CreateDirectory(_offlineStoragePath);
        
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };
        
        InitializeLocalSnapshotId();
    }

    private void InitializeLocalSnapshotId()
    {
        try
        {
            var counterFile = Path.Combine(_offlineStoragePath, "snapshot_counter.txt");
            if (File.Exists(counterFile))
            {
                var counterText = File.ReadAllText(counterFile);
                if (long.TryParse(counterText, out var counter))
                {
                    _nextLocalSnapshotId = counter + 1;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read snapshot counter, starting from 1");
            _nextLocalSnapshotId = 1;
        }
    }

    private async Task UpdateSnapshotCounterAsync()
    {
        try
        {
            var counterFile = Path.Combine(_offlineStoragePath, "snapshot_counter.txt");
            await File.WriteAllTextAsync(counterFile, _nextLocalSnapshotId.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update snapshot counter");
        }
    }

    public long GetNextLocalSnapshotId()
    {
        return Interlocked.Increment(ref _nextLocalSnapshotId);
    }

    public async Task SaveOfflineSnapshotAsync(OfflineSnapshotBatch batch)
    {
        await _fileLock.WaitAsync();
        try
        {
            var fileName = $"snapshot_{batch.BatchId}{batch.Timestamp:yyyyMMdd_HHmmss}.json";
            var filePath = Path.Combine(_offlineStoragePath, fileName);
            
            var jsonContent = JsonSerializer.Serialize(batch, _jsonOptions);
            await File.WriteAllTextAsync(filePath, jsonContent);
            
            await UpdateSnapshotCounterAsync();
            
            _logger.LogInformation("Saved offline snapshot batch {BatchId} with {ProcessCount} processes to {FileName}", 
                batch.BatchId, batch.ProcessSnapshots.Count, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save offline snapshot batch {BatchId}", batch.BatchId);
            throw;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<List<OfflineSnapshotBatch>> GetPendingOfflineSnapshotsAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            var batches = new List<OfflineSnapshotBatch>();
            var jsonFiles = Directory.GetFiles(_offlineStoragePath, "snapshot_*.json")
                                   .OrderBy(f => File.GetCreationTime(f))
                                   .ToList();

            foreach (var file in jsonFiles)
            {
                try
                {
                    var jsonContent = await File.ReadAllTextAsync(file);
                    var batch = JsonSerializer.Deserialize<OfflineSnapshotBatch>(jsonContent, _jsonOptions);
                    if (batch != null)
                    {
                        batches.Add(batch);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize offline snapshot from {FileName}", Path.GetFileName(file));
                }
            }

            return batches;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task RemoveOfflineSnapshotAsync(Guid batchId)
    {
        await _fileLock.WaitAsync();
        try
        {
            var jsonFiles = Directory.GetFiles(_offlineStoragePath, $"snapshot_{batchId}*.json");
            
            foreach (var file in jsonFiles)
            {
                try
                {
                    File.Delete(file);
                    _logger.LogDebug("Removed offline snapshot file {FileName}", Path.GetFileName(file));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete offline snapshot file {FileName}", Path.GetFileName(file));
                }
            }
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<bool> IsRecoveryNeededAsync()
    {
        var fileCount = await GetPendingSnapshotCountAsync();
        return fileCount > 0;
    }

    public async Task<int> GetPendingSnapshotCountAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            var jsonFiles = Directory.GetFiles(_offlineStoragePath, "snapshot_*.json");
            return jsonFiles.Length;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task CleanupOldOfflineDataAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-_maxRetentionDays);
            var jsonFiles = Directory.GetFiles(_offlineStoragePath, "snapshot_*.json");
            
            int deletedCount = 0;
            foreach (var file in jsonFiles)
            {
                try
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.CreationTimeUtc < cutoffDate)
                    {
                        File.Delete(file);
                        deletedCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete old offline file {FileName}", Path.GetFileName(file));
                }
            }

            if (deletedCount > 0)
            {
                _logger.LogInformation("Cleaned up {DeletedCount} old offline snapshot files", deletedCount);
            }
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public void Dispose()
    {
        _fileLock?.Dispose();
    }
}