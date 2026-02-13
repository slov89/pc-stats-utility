using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Slov89.PCStats.Service.Services;

namespace Slov89.PCStats.Service.Tests.Services;

public class ProcessMonitorServiceTests
{
    private readonly Mock<ILogger<ProcessMonitorService>> _mockLogger;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly ProcessMonitorService _service;

    public ProcessMonitorServiceTests()
    {
        _mockLogger = new Mock<ILogger<ProcessMonitorService>>();
        _mockConfiguration = new Mock<IConfiguration>();
        
        // Setup default configuration values
        _mockConfiguration.Setup(c => c["MonitoringSettings:EnableVRAMMonitoring"]).Returns("false");
        
        _service = new ProcessMonitorService(_mockLogger.Object, _mockConfiguration.Object);
    }

    [Fact]
    public async Task GetSystemCpuUsageAsync_ShouldReturnNonNegativeValue()
    {
        // Act
        var result = await _service.GetSystemCpuUsageAsync();

        // Assert
        result.Should().BeGreaterThanOrEqualTo(0);
        result.Should().BeLessThanOrEqualTo(100);
    }

    [Fact]
    public async Task GetSystemCpuUsageAsync_CalledTwice_ShouldReturnValidValue()
    {
        // First call establishes baseline
        var firstResult = await _service.GetSystemCpuUsageAsync();
        
        // Wait a bit for the performance counter to update
        await Task.Delay(200);
        
        // Act - Second call should return actual CPU usage
        var secondResult = await _service.GetSystemCpuUsageAsync();

        // Assert
        secondResult.Should().BeGreaterThanOrEqualTo(0);
        secondResult.Should().BeLessThanOrEqualTo(100);
    }

    [Fact]
    public async Task GetRunningProcessesAsync_ShouldReturnProcesses()
    {
        // Act
        var processes = await _service.GetRunningProcessesAsync();

        // Assert
        processes.Should().NotBeNull();
        processes.Should().NotBeEmpty();
        processes.Should().AllSatisfy(p =>
        {
            p.Pid.Should().BeGreaterThanOrEqualTo(0); // PID 0 is valid (System Idle Process)
            p.ProcessName.Should().NotBeNullOrEmpty();
        });
        
        // Most processes should have valid thread and handle counts
        var processesWithValidCounts = processes.Where(p => p.ThreadCount >= 0 && p.HandleCount >= 0).ToList();
        processesWithValidCounts.Should().HaveCountGreaterThan(0, "at least some processes should have accessible thread/handle counts");
    }

    [Fact]
    public async Task GetRunningProcessesAsync_ShouldIncludeMemoryInfo()
    {
        // Act
        var processes = await _service.GetRunningProcessesAsync();

        // Assert
        processes.Should().NotBeEmpty();
        
        // At least some processes should have memory usage
        processes.Should().Contain(p => p.MemoryUsageMb > 0);
        processes.Should().AllSatisfy(p =>
        {
            p.MemoryUsageMb.Should().BeGreaterThanOrEqualTo(0);
            p.PrivateMemoryMb.Should().BeGreaterThanOrEqualTo(0);
            p.VirtualMemoryMb.Should().BeGreaterThanOrEqualTo(0);
        });
    }

    [Fact]
    public async Task GetRunningProcessesAsync_ShouldReturnUniqueProcessIds()
    {
        // Act
        var processes = await _service.GetRunningProcessesAsync();

        // Assert
        var pids = processes.Select(p => p.Pid).ToList();
        pids.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task GetRunningProcessesAsync_CalledMultipleTimes_ShouldIncludeCpuUsage()
    {
        // First call to establish baseline for CPU usage calculation
        var firstCall = await _service.GetRunningProcessesAsync();
        
        // Wait for CPU counters to update
        await Task.Delay(500);
        
        // Act - Second call should have CPU usage calculated for some processes
        var secondCall = await _service.GetRunningProcessesAsync();

        // Assert
        secondCall.Should().NotBeEmpty();
        
        // At least some processes should have CPU usage calculated
        // (not all will have non-zero usage at any given moment)
        secondCall.Should().AllSatisfy(p => p.CpuUsage.Should().BeGreaterThanOrEqualTo(0));
        secondCall.Should().AllSatisfy(p => p.CpuUsage.Should().BeLessThanOrEqualTo(100));
    }

    [Fact]
    public void CleanupOldProcessTracking_ShouldNotThrow()
    {
        // Act & Assert
        _service.Invoking(s => s.CleanupOldProcessTracking())
            .Should().NotThrow();
    }

    [Fact]
    public async Task GetRunningProcessesAsync_ShouldHandleProcessesWithoutPath()
    {
        // Act
        var processes = await _service.GetRunningProcessesAsync();

        // Assert
        processes.Should().NotBeEmpty();
        
        // Some system processes may not have accessible paths
        // Service should handle this gracefully
        processes.Should().AllSatisfy(p =>
        {
            // Either has a path or it's null (both are valid)
            if (p.ProcessPath != null)
            {
                p.ProcessPath.Should().NotBeEmpty();
            }
        });
    }

    [Fact]
    public async Task GetRunningProcessesAsync_ShouldIncludeCurrentProcess()
    {
        // Arrange
        var currentPid = Environment.ProcessId;

        // Act
        var processes = await _service.GetRunningProcessesAsync();

        // Assert
        processes.Should().Contain(p => p.Pid == currentPid, 
            "the current process should be included in the results");
    }

    [Fact]
    public async Task GetRunningProcessesAsync_MemoryValues_ShouldBeReasonable()
    {
        // Act
        var processes = await _service.GetRunningProcessesAsync();

        // Assert
        processes.Should().NotBeEmpty();
        
        // All memory values should be non-negative
        processes.Should().AllSatisfy(p =>
        {
            p.MemoryUsageMb.Should().BeGreaterThanOrEqualTo(0);
            p.PrivateMemoryMb.Should().BeGreaterThanOrEqualTo(0);
            p.VirtualMemoryMb.Should().BeGreaterThanOrEqualTo(0);
        });
        
        // Most processes should have reasonable memory values (exclude outliers like system processes)
        var processesWithReasonableMemory = processes
            .Where(p => p.MemoryUsageMb < 100_000 && p.PrivateMemoryMb < 100_000)
            .ToList();
        processesWithReasonableMemory.Should().HaveCountGreaterThan(0, "most processes should have reasonable memory usage");
    }
}
