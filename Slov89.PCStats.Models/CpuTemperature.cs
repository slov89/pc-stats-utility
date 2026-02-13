namespace Slov89.PCStats.Models;

public class CpuTemperature
{
    public long TempId { get; set; }
    public long SnapshotId { get; set; }
    public decimal? CpuTctlTdie { get; set; }
    public decimal? CpuDieAverage { get; set; }
    public decimal? CpuCcd1Tdie { get; set; }
    public decimal? CpuCcd2Tdie { get; set; }
    public decimal? ThermalLimitPercent { get; set; }
    public bool? ThermalThrottling { get; set; }
}
