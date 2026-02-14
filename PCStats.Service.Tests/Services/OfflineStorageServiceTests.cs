using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using PCStats.Data;
using PCStats.Models;
using PCStats.Service.Services;

namespace PCStats.Service.Tests.Services;

public class OfflineStorageServiceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly Mock<ILogger<OfflineStorageService>> _loggerMock;
    private readonly Mock<IDatabaseService> _databaseServiceMock;
    private readonly IConfiguration _configuration;
    private readonly OfflineStorageService _service;

    public OfflineStorageServiceTests()
    {
        // Create a unique test directory for each test run
        _testDirectory = Path.Combine(Path.GetTempPath(), $"PCStatsTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        _loggerMock = new Mock<ILogger<OfflineStorageService>>();
        _databaseServiceMock = new Mock<IDatabaseService>();

        // Configure the service to use test directory
        var configValues = new Dictionary<string, string>
        {
            {"OfflineStorage:Path", _testDirectory},
            {"OfflineStorage:MaxRetentionDays", "7"}
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues!)
            .Build();

        _service = new OfflineStorageService(
            _loggerMock.Object,
            _configuration);
    }

    public void Dispose()
    {
        // Clean up test directory
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public async Task SaveOfflineSnapshotAsync_ShouldCreateJsonFile()
    {
        // Arrange
        var batch = new OfflineSnapshotBatch
        {
            LocalSnapshotId = 1,
            SnapshotData = new OfflineSnapshotData
            {
                TotalCpuUsage = 25.5m,
                TotalMemoryMb = 8192,
                AvailableMemoryMb = 4096,
                LocalSnapshotId = 1
            }
        };

        // Act
        await _service.SaveOfflineSnapshotAsync(batch);

        // Assert
        var files = Directory.GetFiles(_testDirectory, "snapshot_*.json");
        files.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetPendingOfflineSnapshotsAsync_ShouldReturnAllBatches()
    {
        // Arrange
        var batch1 = CreateTestBatch(1);
        var batch2 = CreateTestBatch(2);

        await _service.SaveOfflineSnapshotAsync(batch1);
        await _service.SaveOfflineSnapshotAsync(batch2);

        // Act
        var result = await _service.GetPendingOfflineSnapshotsAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(b => b.LocalSnapshotId == 1);
        result.Should().Contain(b => b.LocalSnapshotId == 2);
    }

    [Fact]
    public async Task RemoveOfflineSnapshotAsync_ShouldDeleteFile()
    {
        // Arrange
        var batch = CreateTestBatch(1);
        await _service.SaveOfflineSnapshotAsync(batch);

        var filesBefore = Directory.GetFiles(_testDirectory, "snapshot_*.json");
        filesBefore.Should().HaveCount(1);

        // Act
        await _service.RemoveOfflineSnapshotAsync(batch.BatchId);

        // Assert
        var filesAfter = Directory.GetFiles(_testDirectory, "snapshot_*.json");
        filesAfter.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPendingSnapshotCountAsync_ShouldReturnCorrectCount()
    {
        // Arrange
        await _service.SaveOfflineSnapshotAsync(CreateTestBatch(1));
        await _service.SaveOfflineSnapshotAsync(CreateTestBatch(2));
        await _service.SaveOfflineSnapshotAsync(CreateTestBatch(3));

        // Act
        var count = await _service.GetPendingSnapshotCountAsync();

        // Assert
        count.Should().Be(3);
    }

    [Fact]
    public async Task IsRecoveryNeededAsync_ShouldReturnTrue_WhenFilesExist()
    {
        // Arrange
        await _service.SaveOfflineSnapshotAsync(CreateTestBatch(1));

        // Act
        var result = await _service.IsRecoveryNeededAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsRecoveryNeededAsync_ShouldReturnFalse_WhenNoFiles()
    {
        // Act
        var result = await _service.IsRecoveryNeededAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GetNextLocalSnapshotId_ShouldReturnIncrementingValues()
    {
        // Act
        var id1 = _service.GetNextLocalSnapshotId();
        var id2 = _service.GetNextLocalSnapshotId();
        var id3 = _service.GetNextLocalSnapshotId();

        // Assert
        id2.Should().BeGreaterThan(id1);
        id3.Should().BeGreaterThan(id2);
        (id3 - id1).Should().Be(2);
    }

    [Fact]
    public async Task CleanupOldOfflineDataAsync_ShouldRemoveOldFiles()
    {
        // Arrange
        var oldBatch = CreateTestBatch(1);
        await _service.SaveOfflineSnapshotAsync(oldBatch);

        // Manually modify the file creation time to be 10 days old
        var files = Directory.GetFiles(_testDirectory, "snapshot_*.json");
        File.SetCreationTimeUtc(files[0], DateTime.UtcNow.AddDays(-10));

        // Act
        await _service.CleanupOldOfflineDataAsync();

        // Assert
        var remainingFiles = Directory.GetFiles(_testDirectory, "snapshot_*.json");
        remainingFiles.Should().BeEmpty();
    }

    [Fact]
    public async Task CleanupOldOfflineDataAsync_ShouldKeepRecentFiles()
    {
        // Arrange
        var recentBatch = CreateTestBatch(1);
        await _service.SaveOfflineSnapshotAsync(recentBatch);

        // Act
        await _service.CleanupOldOfflineDataAsync();

        // Assert
        var files = Directory.GetFiles(_testDirectory, "snapshot_*.json");
        files.Should().HaveCount(1);
    }

    [Fact]
    public async Task SaveOfflineSnapshotAsync_ShouldIncludeProcessSnapshots()
    {
        // Arrange
        var batch = new OfflineSnapshotBatch
        {
            LocalSnapshotId = 1,
            SnapshotData = new OfflineSnapshotData { LocalSnapshotId = 1 },
            ProcessSnapshots = new List<OfflineProcessSnapshotData>
            {
                new()
                {
                    ProcessName = "chrome.exe",
                    ProcessPath = "C:\\Program Files\\Chrome\\chrome.exe",
                    ProcessInfo = new ProcessInfo
                    {
                        ProcessName = "chrome.exe",
                        Pid = 1234,
                        CpuUsage = 15.5m,
                        MemoryUsageMb = 512
                    }
                }
            }
        };

        // Act
        await _service.SaveOfflineSnapshotAsync(batch);
        var recovered = await _service.GetPendingOfflineSnapshotsAsync();

        // Assert
        recovered.Should().HaveCount(1);
        recovered[0].ProcessSnapshots.Should().HaveCount(1);
        recovered[0].ProcessSnapshots[0].ProcessName.Should().Be("chrome.exe");
        recovered[0].ProcessSnapshots[0].ProcessInfo.CpuUsage.Should().Be(15.5m);
    }

    [Fact]
    public async Task SaveOfflineSnapshotAsync_ShouldIncludeCpuTemperature()
    {
        // Arrange
        var batch = new OfflineSnapshotBatch
        {
            LocalSnapshotId = 1,
            SnapshotData = new OfflineSnapshotData { LocalSnapshotId = 1 },
            CpuTemperature = new OfflineCpuTemperatureData
            {
                LocalSnapshotId = 1,
                Temperature = new CpuTemperature
                {
                    CpuTctlTdie = 65.5m,
                    CpuDieAverage = 62.0m
                }
            }
        };

        // Act
        await _service.SaveOfflineSnapshotAsync(batch);
        var recovered = await _service.GetPendingOfflineSnapshotsAsync();

        // Assert
        recovered.Should().HaveCount(1);
        recovered[0].CpuTemperature.Should().NotBeNull();
        recovered[0].CpuTemperature!.Temperature.CpuTctlTdie.Should().Be(65.5m);
    }

    [Fact]
    public async Task SaveOfflineSnapshotAsync_WithMultipleBatches_ShouldMaintainOrder()
    {
        // Arrange
        var batch1 = CreateTestBatch(1);
        var batch2 = CreateTestBatch(2);
        var batch3 = CreateTestBatch(3);

        // Act
        await _service.SaveOfflineSnapshotAsync(batch1);
        await Task.Delay(10);
        await _service.SaveOfflineSnapshotAsync(batch2);
        await Task.Delay(10);
        await _service.SaveOfflineSnapshotAsync(batch3);

        var recovered = await _service.GetPendingOfflineSnapshotsAsync();

        // Assert
        recovered.Should().HaveCount(3);
        recovered[0].LocalSnapshotId.Should().Be(1);
        recovered[1].LocalSnapshotId.Should().Be(2);
        recovered[2].LocalSnapshotId.Should().Be(3);
    }

    [Fact]
    public async Task RemoveOfflineSnapshotAsync_WithNonExistentFile_ShouldNotThrow()
    {
        // Act & Assert
        await _service.Invoking(s => s.RemoveOfflineSnapshotAsync(Guid.NewGuid()))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetPendingOfflineSnapshotsAsync_WithEmptyDirectory_ShouldReturnEmptyList()
    {
        // Act
        var result = await _service.GetPendingOfflineSnapshotsAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveOfflineSnapshotAsync_WithNullProcessPath_ShouldHandleGracefully()
    {
        // Arrange
        var batch = new OfflineSnapshotBatch
        {
            LocalSnapshotId = 1,
            SnapshotData = new OfflineSnapshotData { LocalSnapshotId = 1 },
            ProcessSnapshots = new List<OfflineProcessSnapshotData>
            {
                new OfflineProcessSnapshotData
                {
                    LocalSnapshotId = 1,
                    LocalProcessId = 1,
                    ProcessName = "System",
                    ProcessPath = null,
                    ProcessInfo = new ProcessInfo
                    {
                        ProcessName = "System",
                        ProcessPath = null,
                        Pid = 0
                    }
                }
            }
        };

        // Act
        await _service.SaveOfflineSnapshotAsync(batch);
        var recovered = await _service.GetPendingOfflineSnapshotsAsync();

        // Assert
        recovered.Should().HaveCount(1);
        recovered[0].ProcessSnapshots.Should().HaveCount(1);
        recovered[0].ProcessSnapshots[0].ProcessPath.Should().BeNull();
    }

    private OfflineSnapshotBatch CreateTestBatch(long snapshotId)
    {
        return new OfflineSnapshotBatch
        {
            LocalSnapshotId = snapshotId,
            SnapshotData = new OfflineSnapshotData
            {
                LocalSnapshotId = snapshotId,
                TotalCpuUsage = 25.5m,
                TotalMemoryMb = 8192,
                AvailableMemoryMb = 4096,
                Timestamp = DateTime.UtcNow
            }
        };
    }
}
