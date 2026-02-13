using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Slov89.PCStats.Models;

namespace Slov89.PCStats.Service.Services;

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
            // Performance counters need a baseline measurement
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
                    // Some processes may not be accessible (system processes, permission issues)
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
            // Get basic process information
            var processInfo = new ProcessInfo
            {
                Pid = process.Id,
                ProcessName = process.ProcessName,
                ThreadCount = process.Threads.Count,
                HandleCount = process.HandleCount
            };

            // Try to get process path
            try
            {
                processInfo.ProcessPath = process.MainModule?.FileName;
            }
            catch
            {
                // Some processes don't allow access to MainModule
                processInfo.ProcessPath = null;
            }

            // Get memory information (in MB)
            processInfo.MemoryUsageMb = process.WorkingSet64 / (1024 * 1024);
            processInfo.PrivateMemoryMb = process.PrivateMemorySize64 / (1024 * 1024);
            processInfo.VirtualMemoryMb = process.PagedMemorySize64 / (1024 * 1024); // Paged memory (not virtual address space)

            // Calculate CPU usage
            processInfo.CpuUsage = CalculateProcessCpuUsage(process);

            // Get VRAM usage using Performance Counters
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
                    return (decimal)Math.Min(cpuUsagePercent, 100); // Cap at 100%
                }
            }

            // First measurement for this process
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
            // Use Windows Performance Counters to get GPU dedicated memory per process
            // This is what Process Explorer uses
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
                    // Instance names are in format: "pid_XXXXX_luid_0x00000000_0x00013523_phys_0"
                    if (instanceName.StartsWith("pid_"))
                    {
                        var parts = instanceName.Split('_');
                        if (parts.Length >= 2 && int.TryParse(parts[1], out int pid) && pid == processId)
                        {
                            using var counter = new PerformanceCounter(categoryName, "Dedicated Usage", instanceName, true);
                            var bytes = counter.NextValue();
                            
                            // Convert bytes to MB
                            return (long)(bytes / (1024 * 1024));
                        }
                    }
                }
                catch
                {
                    // Skip instances we can't read
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

    // Cleanup old entries from CPU usage tracking
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
