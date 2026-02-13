using Slov89.PCStats.Models;

namespace Slov89.PCStats.Data;

public interface IMetricsService
{
    Task<List<Snapshot>> GetSnapshotsAsync(DateTime startTime, DateTime endTime);
    Task<List<CpuTemperature>> GetCpuTemperaturesAsync(DateTime startTime, DateTime endTime);
    Task<Dictionary<string, List<(DateTime timestamp, decimal cpuUsage, long memoryMb)>>> GetTopProcessesAsync(DateTime startTime, DateTime endTime, int topCount = 5);
}
