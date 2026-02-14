using Slov89.PCStats.Service.Services;
using Slov89.PCStats.Data;
using Slov89.PCStats.Models;
using System.Diagnostics;

namespace Slov89.PCStats.Service;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IProcessMonitorService _processMonitor;
    private readonly IHWiNFOService _hwinfoService;
    private readonly IDatabaseService _databaseService;
    private readonly IConfiguration _configuration;
    private readonly PerformanceCounter _availableMemoryCounter;
    private readonly decimal _minimumCpuUsagePercent;
    private readonly long _minimumPrivateMemoryMb;
    private readonly bool _enableAutoCleanup;
    private readonly int _cleanupIntervalHours;
    private readonly int _retentionDays;
    private readonly int _intervalSeconds;
    private DateTime _lastCleanupTime = DateTime.MinValue;
    private int _cycleCount = 0;

    public Worker(
        ILogger<Worker> logger,
        IProcessMonitorService processMonitor,
        IHWiNFOService hwinfoService,
        IDatabaseService databaseService,
        IConfiguration configuration)
    {
        _logger = logger;
        _processMonitor = processMonitor;
        _hwinfoService = hwinfoService;
        _databaseService = databaseService;
        _configuration = configuration;
        _availableMemoryCounter = new PerformanceCounter("Memory", "Available MBytes");
        _minimumCpuUsagePercent = _configuration.GetValue<decimal>("MonitoringSettings:MinimumCpuUsagePercent", 5.0m);
        _minimumPrivateMemoryMb = _configuration.GetValue<long>("MonitoringSettings:MinimumPrivateMemoryMb", 100);
        _enableAutoCleanup = _configuration.GetValue<bool>("DatabaseCleanup:EnableAutoCleanup", true);
        _cleanupIntervalHours = _configuration.GetValue<int>("DatabaseCleanup:CleanupIntervalHours", 24);
        _retentionDays = _configuration.GetValue<int>("DatabaseCleanup:RetentionDays", 7);
        _intervalSeconds = _configuration.GetValue<int>("MonitoringSettings:IntervalSeconds", 5);
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("PC Stats Service is starting...");

        // Initialize database connection (will handle offline mode if unavailable)
        await _databaseService.InitializeAsync();

        if (!_hwinfoService.IsHWiNFORunning())
        {
            _logger.LogWarning("HWiNFO is not running. CPU temperature monitoring will not be available.");
            _logger.LogWarning("Please start HWiNFO v8.14 with shared memory enabled for temperature monitoring.");
        }

        _logger.LogInformation("Process filtering thresholds - CPU: {MinCpuPercent}%, Private Memory: {MinMemoryMb}MB (processes meeting either threshold will be saved)", 
            _minimumCpuUsagePercent, _minimumPrivateMemoryMb);

        if (_enableAutoCleanup)
        {
            _logger.LogInformation("Automatic database cleanup enabled - Retention: {RetentionDays} days, Interval: {IntervalHours} hours",
                _retentionDays, _cleanupIntervalHours);
        }
        else
        {
            _logger.LogInformation("Automatic database cleanup disabled");
        }

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PC Stats Service started. Monitoring every {IntervalSeconds} seconds...", _intervalSeconds);

        // Initial delay to let performance counters initialize
        await Task.Delay(1000, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                await CollectAndLogStatsAsync();
                stopwatch.Stop();

                _cycleCount++;
                _logger.LogDebug("Cycle {CycleCount} completed in {ElapsedMs}ms", _cycleCount, stopwatch.ElapsedMilliseconds);

                // Run database cleanup if enabled and interval has elapsed
                if (_enableAutoCleanup)
                {
                    var timeSinceLastCleanup = DateTime.Now - _lastCleanupTime;
                    if (timeSinceLastCleanup.TotalHours >= _cleanupIntervalHours)
                    {
                        await RunDatabaseCleanupAsync();
                    }
                }

                // Cleanup old process tracking every 100 cycles (approximately every 8 minutes)
                if (_cycleCount % 100 == 0 && _processMonitor is ProcessMonitorService pms)
                {
                    pms.CleanupOldProcessTracking();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during stats collection cycle");
            }

            // Calculate remaining delay to maintain consistent interval
            var elapsedMs = stopwatch.ElapsedMilliseconds;
            var targetIntervalMs = _intervalSeconds * 1000;
            var remainingDelayMs = Math.Max(0, targetIntervalMs - (int)elapsedMs);
            
            if (remainingDelayMs > 0)
            {
                await Task.Delay(remainingDelayMs, stoppingToken);
            }
            else
            {
                _logger.LogWarning("Cycle {CycleCount} took {ElapsedMs}ms, exceeding configured interval of {IntervalMs}ms",
                    _cycleCount, elapsedMs, targetIntervalMs);
            }
        }
    }

    private async Task CollectAndLogStatsAsync()
    {
        // Get system-level metrics
        var systemCpuUsage = await _processMonitor.GetSystemCpuUsageAsync();
        var availableMemoryMb = (long)_availableMemoryCounter.NextValue();
        
        // Calculate total memory usage (assuming standard Windows system)
        var totalMemoryInfo = GC.GetGCMemoryInfo();
        var totalMemoryMb = totalMemoryInfo.TotalAvailableMemoryBytes / (1024 * 1024);
        var usedMemoryMb = totalMemoryMb - availableMemoryMb;

        // Get running processes
        var processes = await _processMonitor.GetRunningProcessesAsync();
        _logger.LogDebug("Found {TotalProcessCount} running processes", processes.Count);

        // Filter processes by CPU usage OR private memory threshold for detailed snapshot logging
        var filteredProcesses = processes
            .Where(p => p.CpuUsage >= _minimumCpuUsagePercent || p.PrivateMemoryMb >= _minimumPrivateMemoryMb)
            .ToList();
        
        _logger.LogInformation("Saving detailed metrics for {FilteredCount} of {TotalCount} processes (CPU >= {MinCpu}% OR Memory >= {MinMem}MB)",
            filteredProcesses.Count, processes.Count, _minimumCpuUsagePercent, _minimumPrivateMemoryMb);

        // Batch get/create process IDs for better performance
        var processesToCreate = filteredProcesses
            .Select(p => (p.ProcessName, p.ProcessPath))
            .ToList();

        var processIdMap = await _databaseService.BatchGetOrCreateProcessesAsync(processesToCreate);

        // Build the batch of process snapshots
        var processSnapshots = new List<(int processId, ProcessInfo processInfo)>();
        foreach (var processInfo in filteredProcesses)
        {
            var key = $"{processInfo.ProcessName}|{processInfo.ProcessPath ?? ""}";
            if (processIdMap.TryGetValue(key, out var processId))
            {
                processSnapshots.Add((processId, processInfo));
            }
            else
            {
                _logger.LogWarning("Failed to get process ID for {ProcessName}", processInfo.ProcessName);
            }
        }

        // Get CPU temperatures
        _logger.LogDebug("Attempting to read CPU temperatures from HWiNFO...");
        var temperatures = await _hwinfoService.GetCpuTemperaturesAsync();

        // Create snapshot and all related data atomically in a single transaction
        var snapshotId = await _databaseService.CreateSnapshotWithDataAsync(
            systemCpuUsage,
            usedMemoryMb,
            availableMemoryMb,
            processSnapshots,
            temperatures);

        _logger.LogInformation("Created snapshot {SnapshotId} - System CPU: {CpuUsage}%, Memory Used: {MemoryMb}MB",
            snapshotId, systemCpuUsage, usedMemoryMb);

        // Log temperature information if available
        if (temperatures != null)
        {
            var tempReadings = new List<string>();
            if (temperatures.CpuTctlTdie.HasValue) tempReadings.Add($"Tctl/Tdie: {temperatures.CpuTctlTdie:F1}°C");
            if (temperatures.CpuDieAverage.HasValue) tempReadings.Add($"Die Avg: {temperatures.CpuDieAverage:F1}°C");
            if (temperatures.CpuCcd1Tdie.HasValue) tempReadings.Add($"CCD1: {temperatures.CpuCcd1Tdie:F1}°C");
            if (temperatures.CpuCcd2Tdie.HasValue) tempReadings.Add($"CCD2: {temperatures.CpuCcd2Tdie:F1}°C");
            
            if (tempReadings.Any())
            {
                _logger.LogInformation("CPU Temperatures: {Temps}", string.Join(", ", tempReadings));
            }
            else
            {
                _logger.LogWarning("Temperature object returned but no values were set");
            }
        }
        else
        {
            _logger.LogInformation("No CPU temperature data available from HWiNFO");
        }
    }

    private async Task RunDatabaseCleanupAsync()
    {
        try
        {
            _logger.LogInformation("Starting automatic database cleanup (retention: {RetentionDays} days)...", _retentionDays);
            
            var deletedCount = await _databaseService.CleanupOldSnapshotsAsync(_retentionDays);
            
            _logger.LogInformation("Database cleanup completed. Deleted {DeletedCount} snapshots older than {RetentionDays} days",
                deletedCount, _retentionDays);
            
            _lastCleanupTime = DateTime.Now;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during automatic database cleanup");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("PC Stats Service is stopping...");
        _availableMemoryCounter?.Dispose();
        await base.StopAsync(cancellationToken);
    }
}
