using System.Diagnostics;
using System.Management;
using PCStatsService.Models;

namespace PCStatsService.Services;

public interface IProcessMonitorService
{
    Task<List<ProcessInfo>> GetRunningProcessesAsync();
    Task<decimal> GetSystemCpuUsageAsync();
}

public class ProcessMonitorService : IProcessMonitorService
{
    private readonly ILogger<ProcessMonitorService> _logger;
    private readonly PerformanceCounter _cpuCounter;
    private readonly PerformanceCounter _ramCounter;
    private DateTime _lastCpuCheck = DateTime.MinValue;
    private readonly Dictionary<int, (DateTime lastCheck, TimeSpan lastTotalProcessorTime)> _processCpuUsage = new();

    public ProcessMonitorService(ILogger<ProcessMonitorService> logger)
    {
        _logger = logger;
        _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        _ramCounter = new PerformanceCounter("Memory", "Available MBytes");
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
            processInfo.VirtualMemoryMb = process.VirtualMemorySize64 / (1024 * 1024);

            // Calculate CPU usage
            processInfo.CpuUsage = CalculateProcessCpuUsage(process);

            // Get VRAM usage using WMI
            processInfo.VramUsageMb = await GetProcessVramUsageAsync(process.Id);

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

    private async Task<long> GetProcessVramUsageAsync(int processId)
    {
        try
        {
            // Try to get VRAM usage via WMI (this may not work for all GPUs/drivers)
            using var searcher = new ManagementObjectSearcher(
                $"SELECT * FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine WHERE ProcessId = {processId}");
            
            long totalVram = 0;
            var results = await Task.Run(() => searcher.Get());
            
            foreach (ManagementObject obj in results)
            {
                try
                {
                    var dedicatedUsage = obj["DedicatedUsage"];
                    if (dedicatedUsage != null)
                    {
                        totalVram += Convert.ToInt64(dedicatedUsage);
                    }
                }
                catch
                {
                    // Ignore individual failures
                }
            }

            return totalVram / (1024 * 1024); // Convert to MB
        }
        catch
        {
            // VRAM monitoring may not be available on all systems
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
