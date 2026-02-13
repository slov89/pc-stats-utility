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
}
