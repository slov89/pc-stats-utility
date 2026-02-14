using Slov89.PCStats.Models;

namespace Slov89.PCStats.Data;

public interface IMetricsService
{
    Task<List<Snapshot>> GetSnapshotsAsync(DateTime startTime, DateTime endTime);
    Task<List<CpuTemperature>> GetCpuTemperaturesAsync(DateTime startTime, DateTime endTime);
    Task<Dictionary<string, List<(DateTime timestamp, decimal cpuUsage, long memoryMb)>>> GetTopProcessesAsync(DateTime startTime, DateTime endTime, int topCount = 5);
    Task<Dictionary<long, List<ProcessSnapshotWithName>>> GetProcessSnapshotsAsync(DateTime startTime, DateTime endTime);
    Task<List<ProcessSnapshotDetail>> GetLatestProcessSnapshotsAsync();
}

public class ProcessSnapshotWithName
{
    public long SnapshotId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public long? PrivateMemoryMb { get; set; }
    public long? MemoryUsageMb { get; set; }
    public decimal? CpuUsage { get; set; }
    public int ProcessCount { get; set; }
}

public class ProcessSnapshotDetail
{
    public long ProcessSnapshotId { get; set; }
    public int Pid { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string? ProcessPath { get; set; }
    public decimal? CpuUsage { get; set; }
    public long? PrivateMemoryMb { get; set; }
    public long? VramUsageMb { get; set; }
    public int? ThreadCount { get; set; }
    public int? HandleCount { get; set; }
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
}
