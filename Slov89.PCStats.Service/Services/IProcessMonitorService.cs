using Slov89.PCStats.Models;

namespace Slov89.PCStats.Service.Services;

public interface IProcessMonitorService
{
    Task<List<ProcessInfo>> GetRunningProcessesAsync();
    Task<decimal> GetSystemCpuUsageAsync();
}
