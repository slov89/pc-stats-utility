namespace PCStatsService.Models;

public class ProcessSnapshot
{
    public long ProcessSnapshotId { get; set; }
    public long SnapshotId { get; set; }
    public int ProcessId { get; set; }
    public int Pid { get; set; }
    public decimal? CpuUsage { get; set; }
    public long? MemoryUsageMb { get; set; }
    public long? PrivateMemoryMb { get; set; }
    public long? VirtualMemoryMb { get; set; }
    public long? VramUsageMb { get; set; }
    public int? ThreadCount { get; set; }
    public int? HandleCount { get; set; }
}
