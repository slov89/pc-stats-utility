using FluentAssertions;
using Slov89.PCStats.Models;

namespace Slov89.PCStats.Data.Tests.Models;

public class ModelValidationTests
{
    [Fact]
    public void ProcessInfo_ShouldHaveRequiredProperties()
    {
        // Arrange & Act
        var processInfo = new ProcessInfo
        {
            ProcessName = "test.exe",
            ProcessPath = "C:\\test.exe",
            Pid = 1234,
            CpuUsage = 10.5m,
            MemoryUsageMb = 100,
            PrivateMemoryMb = 80,
            VirtualMemoryMb = 200,
            VramUsageMb = 50,
            ThreadCount = 10,
            HandleCount = 50
        };

        // Assert
        processInfo.ProcessName.Should().Be("test.exe");
        processInfo.ProcessPath.Should().Be("C:\\test.exe");
        processInfo.Pid.Should().Be(1234);
        processInfo.CpuUsage.Should().Be(10.5m);
        processInfo.MemoryUsageMb.Should().Be(100);
        processInfo.PrivateMemoryMb.Should().Be(80);
        processInfo.VirtualMemoryMb.Should().Be(200);
        processInfo.VramUsageMb.Should().Be(50);
        processInfo.ThreadCount.Should().Be(10);
        processInfo.HandleCount.Should().Be(50);
    }

    [Fact]
    public void ProcessInfo_WithNullPath_ShouldBeValid()
    {
        // Arrange & Act
        var processInfo = new ProcessInfo
        {
            ProcessName = "test.exe",
            ProcessPath = null,
            Pid = 1234
        };

        // Assert
        processInfo.ProcessName.Should().Be("test.exe");
        processInfo.ProcessPath.Should().BeNull();
    }

    [Fact]
    public void CpuTemperature_ShouldSupportNullableValues()
    {
        // Arrange & Act
        var temp = new CpuTemperature
        {
            CpuTctlTdie = 65.5m,
            CpuDieAverage = null,
            CpuCcd1Tdie = 64.0m,
            CpuCcd2Tdie = null,
            ThermalLimitPercent = 50.0m,
            ThermalThrottling = false
        };

        // Assert
        temp.CpuTctlTdie.Should().Be(65.5m);
        temp.CpuDieAverage.Should().BeNull();
        temp.CpuCcd1Tdie.Should().Be(64.0m);
        temp.CpuCcd2Tdie.Should().BeNull();
        temp.ThermalLimitPercent.Should().Be(50.0m);
        temp.ThermalThrottling.Should().BeFalse();
    }

    [Fact]
    public void CpuTemperature_AllNullValues_ShouldBeValid()
    {
        // Arrange & Act
        var temp = new CpuTemperature
        {
            CpuTctlTdie = null,
            CpuDieAverage = null,
            CpuCcd1Tdie = null,
            CpuCcd2Tdie = null,
            ThermalLimitPercent = null,
            ThermalThrottling = false
        };

        // Assert
        temp.CpuTctlTdie.Should().BeNull();
        temp.CpuDieAverage.Should().BeNull();
        temp.CpuCcd1Tdie.Should().BeNull();
        temp.CpuCcd2Tdie.Should().BeNull();
        temp.ThermalLimitPercent.Should().BeNull();
    }

    [Fact]
    public void Snapshot_ShouldHaveValidTimestamp()
    {
        // Arrange & Act
        var snapshot = new Snapshot
        {
            SnapshotId = 1,
            SnapshotTimestamp = DateTime.UtcNow,
            TotalCpuUsage = 45.5m,
            TotalMemoryUsageMb = 8192,
            TotalAvailableMemoryMb = 4096
        };

        // Assert
        snapshot.SnapshotTimestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        snapshot.TotalCpuUsage.Should().Be(45.5m);
        snapshot.TotalMemoryUsageMb.Should().Be(8192);
        snapshot.TotalAvailableMemoryMb.Should().Be(4096);
    }

    [Fact]
    public void Process_ShouldHaveIdentityProperties()
    {
        // Arrange & Act
        var process = new Process
        {
            ProcessId = 1,
            ProcessName = "chrome.exe",
            ProcessPath = "C:\\Program Files\\Chrome\\chrome.exe",
            FirstSeen = DateTime.UtcNow,
            LastSeen = DateTime.UtcNow
        };

        // Assert
        process.ProcessId.Should().Be(1);
        process.ProcessName.Should().Be("chrome.exe");
        process.ProcessPath.Should().Be("C:\\Program Files\\Chrome\\chrome.exe");
    }

    [Fact]
    public void ProcessSnapshot_ShouldLinkProcessAndSnapshot()
    {
        // Arrange & Act
        var processSnapshot = new ProcessSnapshot
        {
            ProcessSnapshotId = 1,
            SnapshotId = 100,
            ProcessId = 50,
            Pid = 1234,
            CpuUsage = 15.5m,
            MemoryUsageMb = 512,
            PrivateMemoryMb = 400,
            VirtualMemoryMb = 1024,
            VramUsageMb = 256,
            ThreadCount = 45,
            HandleCount = 230
        };

        // Assert
        processSnapshot.SnapshotId.Should().Be(100);
        processSnapshot.ProcessId.Should().Be(50);
        processSnapshot.CpuUsage.Should().Be(15.5m);
        processSnapshot.MemoryUsageMb.Should().Be(512);
    }

    [Fact]
    public void OfflineSnapshotBatch_ShouldContainSnapshotData()
    {
        // Arrange & Act
        var batch = new OfflineSnapshotBatch
        {
            LocalSnapshotId = 1,
            SnapshotData = new OfflineSnapshotData
            {
                TotalCpuUsage = 45.5m,
                TotalMemoryMb = 8192,
                AvailableMemoryMb = 4096,
                Timestamp = DateTime.UtcNow,
                LocalSnapshotId = 1
            }
        };

        // Assert
        batch.LocalSnapshotId.Should().Be(1);
        batch.SnapshotData.Should().NotBeNull();
        batch.SnapshotData.TotalCpuUsage.Should().Be(45.5m);
    }

    [Fact]
    public void OfflineSnapshotBatch_WithProcessSnapshots_ShouldContainList()
    {
        // Arrange & Act
        var batch = new OfflineSnapshotBatch
        {
            LocalSnapshotId = 1,
            SnapshotData = new OfflineSnapshotData
            {
                TotalCpuUsage = 45.5m,
                TotalMemoryMb = 8192,
                AvailableMemoryMb = 4096,
                LocalSnapshotId = 1
            },
            ProcessSnapshots = new List<OfflineProcessSnapshotData>
            {
                new OfflineProcessSnapshotData
                {
                    LocalSnapshotId = 1,
                    LocalProcessId = 1,
                    ProcessName = "test.exe",
                    ProcessInfo = new ProcessInfo
                    {
                        ProcessName = "test.exe",
                        Pid = 1234
                    }
                }
            }
        };

        // Assert
        batch.ProcessSnapshots.Should().HaveCount(1);
        batch.ProcessSnapshots![0].ProcessName.Should().Be("test.exe");
    }

    [Fact]
    public void OfflineSnapshotBatch_WithCpuTemperature_ShouldContainTemperatureData()
    {
        // Arrange & Act
        var batch = new OfflineSnapshotBatch
        {
            LocalSnapshotId = 1,
            SnapshotData = new OfflineSnapshotData
            {
                TotalCpuUsage = 45.5m,
                TotalMemoryMb = 8192,
                AvailableMemoryMb = 4096,
                LocalSnapshotId = 1
            },
            CpuTemperature = new OfflineCpuTemperatureData
            {
                LocalSnapshotId = 1,
                Temperature = new CpuTemperature
                {
                    CpuTctlTdie = 65.5m
                }
            }
        };

        // Assert
        batch.CpuTemperature.Should().NotBeNull();
        batch.CpuTemperature!.Temperature.CpuTctlTdie.Should().Be(65.5m);
    }

    [Fact]
    public void OfflineOperation_ShouldHaveOperationType()
    {
        // Arrange & Act
        var operation = new OfflineOperation
        {
            OperationId = Guid.NewGuid(),
            OperationType = OfflineOperationType.CreateSnapshot,
            Timestamp = DateTime.UtcNow,
            Data = new { test = "data" },
            RetryCount = 0
        };

        // Assert
        operation.OperationType.Should().Be(OfflineOperationType.CreateSnapshot);
        operation.ErrorMessage.Should().BeNull();
        operation.RetryCount.Should().Be(0);
    }

    [Fact]
    public void OfflineProcessData_ShouldHaveLocalProcessId()
    {
        // Arrange & Act
        var processData = new OfflineProcessData
        {
            LocalProcessId = 1,
            ProcessName = "test.exe",
            ProcessPath = "C:\\test.exe"
        };

        // Assert
        processData.LocalProcessId.Should().Be(1);
        processData.ProcessName.Should().Be("test.exe");
        processData.ProcessPath.Should().Be("C:\\test.exe");
    }

    [Fact]
    public void OfflineBatchProcessSnapshotsData_ShouldContainSnapshotIdAndProcesses()
    {
        // Arrange & Act
        var batchData = new OfflineBatchProcessSnapshotsData
        {
            LocalSnapshotId = 1,
            ProcessSnapshots = new List<OfflineProcessSnapshotData>
            {
                new OfflineProcessSnapshotData
                {
                    LocalSnapshotId = 1,
                    LocalProcessId = 1,
                    ProcessName = "test.exe",
                    ProcessInfo = new ProcessInfo { ProcessName = "test.exe", Pid = 1234 }
                }
            }
        };

        // Assert
        batchData.LocalSnapshotId.Should().Be(1);
        batchData.ProcessSnapshots.Should().HaveCount(1);
    }
}
