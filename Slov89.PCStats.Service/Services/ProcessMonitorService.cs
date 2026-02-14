using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Slov89.PCStats.Models;

namespace Slov89.PCStats.Service.Services;

/// <summary>
/// Monitors system and process-level resource usage using Windows performance counters
/// </summary>
public class ProcessMonitorService : IProcessMonitorService
{
    private readonly ILogger<ProcessMonitorService> _logger;
    private readonly PerformanceCounter _cpuCounter;
    private readonly PerformanceCounter _ramCounter;
    private readonly bool _enableVramMonitoring;
    private DateTime _lastCpuCheck = DateTime.MinValue;
    private readonly Dictionary<int, (DateTime lastCheck, TimeSpan lastTotalProcessorTime)> _processCpuUsage = new();

    public ProcessMonitorService(ILogger<ProcessMonitorService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        _ramCounter = new PerformanceCounter("Memory", "Available MBytes");
        _enableVramMonitoring = configuration.GetValue<bool>("MonitoringSettings:EnableVRAMMonitoring", true);
    }

    public async Task<decimal> GetSystemCpuUsageAsync()
    {
        try
        {
            if (_lastCpuCheck == DateTime.MinValue)
            {
                _cpuCounter.NextValue();
                _lastCpuCheck = DateTime.Now;
                await Task.Delay(100);
            }

            return (decimal)_cpuCounter.NextValue();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting system CPU usage");
            return 0;
        }
    }

    public async Task<List<ProcessInfo>> GetRunningProcessesAsync()
    {
        var processInfoList = new List<ProcessInfo>();

        try
        {
            var processes = System.Diagnostics.Process.GetProcesses();
            _logger.LogDebug("Found {ProcessCount} running processes", processes.Length);

            foreach (var process in processes)
            {
                try
                {
                    var processInfo = await GetProcessInfoAsync(process);
                    if (processInfo != null)
                    {
                        processInfoList.Add(processInfo);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogTrace(ex, "Could not access process {ProcessName} (PID: {Pid})", 
                        process.ProcessName, process.Id);
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting running processes");
        }

        return processInfoList;
    }

    private async Task<ProcessInfo?> GetProcessInfoAsync(System.Diagnostics.Process process)
    {
        try
        {
            var processInfo = new ProcessInfo
            {
                Pid = process.Id,
                ProcessName = process.ProcessName,
                ThreadCount = process.Threads.Count,
                HandleCount = process.HandleCount
            };

            try
            {
                processInfo.ProcessPath = process.MainModule?.FileName;
            }
            catch
            {
                processInfo.ProcessPath = null;
            }

            processInfo.MemoryUsageMb = process.WorkingSet64 / (1024 * 1024);
            processInfo.PrivateMemoryMb = process.PrivateMemorySize64 / (1024 * 1024);
            processInfo.VirtualMemoryMb = process.PagedMemorySize64 / (1024 * 1024);

            processInfo.CpuUsage = CalculateProcessCpuUsage(process);

            processInfo.VramUsageMb = GetProcessVramUsage(process.Id);

            return processInfo;
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Error getting info for process {ProcessName} (PID: {Pid})", 
                process.ProcessName, process.Id);
            return null;
        }
    }

    private decimal CalculateProcessCpuUsage(System.Diagnostics.Process process)
    {
        try
        {
            var now = DateTime.Now;
            var currentTotalProcessorTime = process.TotalProcessorTime;

            if (_processCpuUsage.TryGetValue(process.Id, out var lastMeasurement))
            {
                var timeDiff = (now - lastMeasurement.lastCheck).TotalMilliseconds;
                if (timeDiff > 0)
                {
                    var cpuDiff = (currentTotalProcessorTime - lastMeasurement.lastTotalProcessorTime).TotalMilliseconds;
                    var cpuUsagePercent = (cpuDiff / (timeDiff * Environment.ProcessorCount)) * 100;
                    
                    _processCpuUsage[process.Id] = (now, currentTotalProcessorTime);
                    return (decimal)Math.Min(cpuUsagePercent, 100);
                }
            }

            _processCpuUsage[process.Id] = (now, currentTotalProcessorTime);
            return 0;
        }
        catch
        {
            return 0;
        }
    }

    private long GetProcessVramUsage(int processId)
    {
        if (!_enableVramMonitoring)
            return 0;

        try
        {
            var categoryName = "GPU Process Memory";
            
            if (!PerformanceCounterCategory.Exists(categoryName))
            {
                _logger.LogDebug("GPU Process Memory performance counter category not found");
                return 0;
            }

            var category = new PerformanceCounterCategory(categoryName);
            var instanceNames = category.GetInstanceNames();

            foreach (var instanceName in instanceNames)
            {
                try
                {
                    if (instanceName.StartsWith("pid_"))
                    {
                        var parts = instanceName.Split('_');
                        if (parts.Length >= 2 && int.TryParse(parts[1], out int pid) && pid == processId)
                        {
                            using var counter = new PerformanceCounter(categoryName, "Dedicated Usage", instanceName, true);
                            var bytes = counter.NextValue();
                            
                            return (long)(bytes / (1024 * 1024));
                        }
                    }
                }
                catch
                {
                    continue;
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get VRAM usage via performance counters for process {ProcessId}", processId);
            return 0;
        }
    }

    public void CleanupOldProcessTracking()
    {
        var currentProcessIds = new HashSet<int>(
            System.Diagnostics.Process.GetProcesses().Select(p => p.Id));

        var keysToRemove = _processCpuUsage.Keys
            .Where(pid => !currentProcessIds.Contains(pid))
            .ToList();

        foreach (var key in keysToRemove)
        {
            _processCpuUsage.Remove(key);
        }
    }
}
