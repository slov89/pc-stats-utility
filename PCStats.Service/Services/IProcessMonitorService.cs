using PCStats.Models;

namespace PCStats.Service.Services;

/// <summary>
/// Provides methods for monitoring running processes and system CPU usage
/// </summary>
public interface IProcessMonitorService
{
    /// <summary>
    /// Gets information about all currently running processes
    /// </summary>
    /// <returns>A list of process information objects</returns>
    Task<List<ProcessInfo>> GetRunningProcessesAsync();
    
    /// <summary>
    /// Gets the current system-wide CPU usage percentage
    /// </summary>
    /// <returns>The total CPU usage across all cores</returns>
    Task<decimal> GetSystemCpuUsageAsync();
}
