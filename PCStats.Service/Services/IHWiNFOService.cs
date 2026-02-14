using PCStats.Models;

namespace PCStats.Service.Services;

/// <summary>
/// Provides methods for reading CPU temperature data from HWiNFO shared memory
/// </summary>
public interface IHWiNFOService
{
    /// <summary>
    /// Gets the current CPU temperature readings from HWiNFO
    /// </summary>
    /// <returns>The CPU temperature data, or null if HWiNFO is not available</returns>
    Task<CpuTemperature?> GetCpuTemperaturesAsync();
    
    /// <summary>
    /// Checks if HWiNFO is currently running with shared memory enabled
    /// </summary>
    /// <returns>True if HWiNFO is running and accessible, false otherwise</returns>
    bool IsHWiNFORunning();
}
