namespace Slov89.PCStats.Models;

public class Snapshot
{
    public long SnapshotId { get; set; }
    public DateTime SnapshotTimestamp { get; set; }
    public decimal? TotalCpuUsage { get; set; }
    public long? TotalMemoryUsageMb { get; set; }
    public long? TotalAvailableMemoryMb { get; set; }
}
