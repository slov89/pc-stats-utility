namespace PCStats.Models;

/// <summary>
/// Represents CPU temperature measurements from hardware monitoring
/// </summary>
public class CpuTemperature
{
    /// <summary>
    /// Gets or sets the unique identifier for this temperature reading
    /// </summary>
    public long TempId { get; set; }
    
    /// <summary>
    /// Gets or sets the snapshot ID this temperature reading belongs to
    /// </summary>
    public long SnapshotId { get; set; }
    
    /// <summary>
    /// Gets or sets the CPU Tctl/Tdie temperature in degrees Celsius
    /// </summary>
    public decimal? CpuTctlTdie { get; set; }
    
    /// <summary>
    /// Gets or sets the average CPU die temperature in degrees Celsius
    /// </summary>
    public decimal? CpuDieAverage { get; set; }
    
    /// <summary>
    /// Gets or sets the CPU CCD1 die temperature in degrees Celsius
    /// </summary>
    public decimal? CpuCcd1Tdie { get; set; }
    
    /// <summary>
    /// Gets or sets the CPU CCD2 die temperature in degrees Celsius
    /// </summary>
    public decimal? CpuCcd2Tdie { get; set; }
    
    /// <summary>
    /// Gets or sets the thermal limit percentage (0-100)
    /// </summary>
    public decimal? ThermalLimitPercent { get; set; }
    
    /// <summary>
    /// Gets or sets whether thermal throttling is currently active
    /// </summary>
    public bool? ThermalThrottling { get; set; }
}
