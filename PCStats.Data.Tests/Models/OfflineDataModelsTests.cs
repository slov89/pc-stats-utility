using FluentAssertions;
using PCStats.Models;
using System.Text.Json;

namespace PCStats.Data.Tests.Models;

public class OfflineDataModelsTests
{
    [Fact]
    public void OfflineSnapshotData_ShouldSerializeAndDeserialize()
    {
        // Arrange
        var original = new OfflineSnapshotData
        {
            TotalCpuUsage = 45.5m,
            TotalMemoryMb = 16384,
            AvailableMemoryMb = 8192,
            Timestamp = DateTime.UtcNow,
            LocalSnapshotId = 123
        };

        // Act
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<OfflineSnapshotData>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.TotalCpuUsage.Should().Be(original.TotalCpuUsage);
        deserialized.TotalMemoryMb.Should().Be(original.TotalMemoryMb);
        deserialized.AvailableMemoryMb.Should().Be(original.AvailableMemoryMb);
        deserialized.LocalSnapshotId.Should().Be(original.LocalSnapshotId);
    }

    [Fact]
    public void OfflineProcessSnapshotData_ShouldSerializeAndDeserialize()
    {
        // Arrange
        var original = new OfflineProcessSnapshotData
        {
            LocalSnapshotId = 123,
            LocalProcessId = 456,
            ProcessName = "chrome.exe",
            ProcessPath = "C:\\Program Files\\Chrome\\chrome.exe",
            ProcessInfo = new ProcessInfo
            {
                ProcessName = "chrome.exe",
                ProcessPath = "C:\\Program Files\\Chrome\\chrome.exe",
                Pid = 1234,
                CpuUsage = 15.5m,
                MemoryUsageMb = 512,
                PrivateMemoryMb = 400,
                VirtualMemoryMb = 1024,
                VramUsageMb = 256,
                ThreadCount = 45,
                HandleCount = 230
            }
        };

        // Act
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<OfflineProcessSnapshotData>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.ProcessName.Should().Be(original.ProcessName);
        deserialized.ProcessPath.Should().Be(original.ProcessPath);
        deserialized.ProcessInfo.Pid.Should().Be(original.ProcessInfo.Pid);
        deserialized.ProcessInfo.CpuUsage.Should().Be(original.ProcessInfo.CpuUsage);
        deserialized.ProcessInfo.MemoryUsageMb.Should().Be(original.ProcessInfo.MemoryUsageMb);
    }

    [Fact]
    public void OfflineCpuTemperatureData_ShouldSerializeAndDeserialize()
    {
        // Arrange
        var original = new OfflineCpuTemperatureData
        {
            LocalSnapshotId = 123,
            Temperature = new CpuTemperature
            {
                CpuTctlTdie = 65.5m,
                CpuDieAverage = 62.0m,
                CpuCcd1Tdie = 64.0m,
                CpuCcd2Tdie = 60.0m,
                ThermalLimitPercent = 75.0m,
                ThermalThrottling = false
            }
        };

        // Act
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<OfflineCpuTemperatureData>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.LocalSnapshotId.Should().Be(original.LocalSnapshotId);
        deserialized.Temperature.CpuTctlTdie.Should().Be(original.Temperature.CpuTctlTdie);
        deserialized.Temperature.CpuDieAverage.Should().Be(original.Temperature.CpuDieAverage);
    }

    [Fact]
    public void OfflineSnapshotBatch_ShouldSerializeCompleteBatch()
    {
        // Arrange
        var original = new OfflineSnapshotBatch
        {
            BatchId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            LocalSnapshotId = 123,
            SnapshotData = new OfflineSnapshotData
            {
                TotalCpuUsage = 45.5m,
                TotalMemoryMb = 16384,
                AvailableMemoryMb = 8192,
                LocalSnapshotId = 123
            },
            ProcessSnapshots = new List<OfflineProcessSnapshotData>
            {
                new()
                {
                    ProcessName = "chrome.exe",
                    ProcessInfo = new ProcessInfo
                    {
                        ProcessName = "chrome.exe",
                        Pid = 1234,
                        CpuUsage = 15.5m,
                        MemoryUsageMb = 512
                    }
                },
                new()
                {
                    ProcessName = "firefox.exe",
                    ProcessInfo = new ProcessInfo
                    {
                        ProcessName = "firefox.exe",
                        Pid = 5678,
                        CpuUsage = 12.3m,
                        MemoryUsageMb = 450
                    }
                }
            },
            CpuTemperature = new OfflineCpuTemperatureData
            {
                LocalSnapshotId = 123,
                Temperature = new CpuTemperature
                {
                    CpuTctlTdie = 65.5m
                }
            },
            RetryCount = 0,
            ErrorMessage = null
        };

        // Act
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<OfflineSnapshotBatch>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.BatchId.Should().Be(original.BatchId);
        deserialized.LocalSnapshotId.Should().Be(original.LocalSnapshotId);
        deserialized.SnapshotData.Should().NotBeNull();
        deserialized.ProcessSnapshots.Should().HaveCount(2);
        deserialized.CpuTemperature.Should().NotBeNull();
        deserialized.RetryCount.Should().Be(0);
    }

    [Fact]
    public void OfflineSnapshotBatch_ShouldHandleNullOptionalFields()
    {
        // Arrange
        var original = new OfflineSnapshotBatch
        {
            LocalSnapshotId = 123,
            SnapshotData = null,
            CpuTemperature = null,
            ErrorMessage = null
        };

        // Act
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<OfflineSnapshotBatch>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.SnapshotData.Should().BeNull();
        deserialized.CpuTemperature.Should().BeNull();
        deserialized.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void ProcessInfo_ShouldSerializeAllProperties()
    {
        // Arrange
        var original = new ProcessInfo
        {
            ProcessName = "test.exe",
            ProcessPath = "C:\\Test\\test.exe",
            Pid = 9999,
            CpuUsage = 25.5m,
            MemoryUsageMb = 1024,
            PrivateMemoryMb = 800,
            VirtualMemoryMb = 2048,
            VramUsageMb = 512,
            ThreadCount = 50,
            HandleCount = 300
        };

        // Act
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<ProcessInfo>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void CpuTemperature_ShouldSerializeAllProperties()
    {
        // Arrange
        var original = new CpuTemperature
        {
            TempId = 1,
            SnapshotId = 100,
            CpuTctlTdie = 70.5m,
            CpuDieAverage = 68.0m,
            CpuCcd1Tdie = 69.0m,
            CpuCcd2Tdie = 67.0m,
            ThermalLimitPercent = 80.0m,
            ThermalThrottling = true
        };

        // Act
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<CpuTemperature>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void OfflineSnapshotBatch_ShouldIncrementRetryCount()
    {
        // Arrange
        var batch = new OfflineSnapshotBatch
        {
            LocalSnapshotId = 1,
            RetryCount = 0,
            ErrorMessage = null
        };

        // Act & Assert
        batch.RetryCount++;
        batch.ErrorMessage = "First error";
        batch.RetryCount.Should().Be(1);

        batch.RetryCount++;
        batch.ErrorMessage = "Second error";
        batch.RetryCount.Should().Be(2);

        batch.RetryCount++;
        batch.ErrorMessage = "Third error";
        batch.RetryCount.Should().Be(3);
    }
}
