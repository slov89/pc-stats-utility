using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using PCStats.Data;
using PCStats.Models;
using PCStats.Service.Services;

namespace PCStats.Service.Tests.Services;

public class OfflineDatabaseServiceTests
{
    private readonly Mock<IDatabaseService> _databaseServiceMock;
    private readonly Mock<IOfflineStorageService> _offlineStorageMock;
    private readonly Mock<ILogger<OfflineDatabaseService>> _loggerMock;
    private readonly OfflineDatabaseService _service;

    public OfflineDatabaseServiceTests()
    {
        _databaseServiceMock = new Mock<IDatabaseService>();
        _offlineStorageMock = new Mock<IOfflineStorageService>();
        _loggerMock = new Mock<ILogger<OfflineDatabaseService>>();

        _service = new OfflineDatabaseService(
            _databaseServiceMock.Object,
            _offlineStorageMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task CreateSnapshotAsync_ShouldCallDatabase_WhenConnectionAvailable()
    {
        // Arrange
        _databaseServiceMock
            .Setup(x => x.CreateSnapshotAsync(It.IsAny<decimal?>(), It.IsAny<long?>(), It.IsAny<long?>()))
            .ReturnsAsync(123L);

        // Act
        var result = await _service.CreateSnapshotAsync(25.5m, 8192, 4096);

        // Assert
        result.Should().Be(123L);
        _databaseServiceMock.Verify(
            x => x.CreateSnapshotAsync(25.5m, 8192, 4096),
            Times.Once);
        _offlineStorageMock.Verify(
            x => x.SaveOfflineSnapshotAsync(It.IsAny<OfflineSnapshotBatch>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateSnapshotAsync_ShouldSwitchToOfflineMode_WhenDatabaseFails()
    {
        // Arrange
        _databaseServiceMock
            .Setup(x => x.CreateSnapshotAsync(It.IsAny<decimal?>(), It.IsAny<long?>(), It.IsAny<long?>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        _offlineStorageMock
            .Setup(x => x.GetNextLocalSnapshotId())
            .Returns(456L);

        // Act
        var result = await _service.CreateSnapshotAsync(25.5m, 8192, 4096);

        // Assert
        result.Should().Be(456L);
        _offlineStorageMock.Verify(
            x => x.SaveOfflineSnapshotAsync(It.Is<OfflineSnapshotBatch>(
                batch => batch.SnapshotData != null &&
                         batch.SnapshotData.TotalCpuUsage == 25.5m &&
                         batch.SnapshotData.TotalMemoryMb == 8192)),
            Times.Once);
    }

    [Fact]
    public async Task GetOrCreateProcessAsync_ShouldCallDatabase_WhenOnline()
    {
        // Arrange
        _databaseServiceMock
            .Setup(x => x.GetOrCreateProcessAsync("chrome.exe", It.IsAny<string?>()))
            .ReturnsAsync(789);

        // Act
        var result = await _service.GetOrCreateProcessAsync("chrome.exe", "C:\\Program Files\\Chrome\\chrome.exe");

        // Assert
        result.Should().Be(789);
        _databaseServiceMock.Verify(
            x => x.GetOrCreateProcessAsync("chrome.exe", "C:\\Program Files\\Chrome\\chrome.exe"),
            Times.Once);
    }

    [Fact]
    public async Task GetOrCreateProcessAsync_ShouldReturnHashedId_WhenOffline()
    {
        // Arrange - First call fails, switches to offline mode
        _databaseServiceMock
            .Setup(x => x.CreateSnapshotAsync(It.IsAny<decimal?>(), It.IsAny<long?>(), It.IsAny<long?>()))
            .ThrowsAsync(new Exception("Database offline"));

        _offlineStorageMock.Setup(x => x.GetNextLocalSnapshotId()).Returns(1L);

        // Force offline mode by failing a snapshot creation
        await _service.CreateSnapshotAsync(10.0m, 1000, 2000);

        // Act
        var result1 = await _service.GetOrCreateProcessAsync("chrome.exe", "C:\\Program Files\\Chrome\\chrome.exe");
        var result2 = await _service.GetOrCreateProcessAsync("chrome.exe", "C:\\Program Files\\Chrome\\chrome.exe");
        var result3 = await _service.GetOrCreateProcessAsync("firefox.exe", "C:\\Program Files\\Firefox\\firefox.exe");

        // Assert
        result1.Should().Be(result2); // Same process should return same ID
        result3.Should().NotBe(result1); // Different process should return different ID
    }

    [Fact]
    public async Task CreateProcessSnapshotAsync_ShouldCallDatabase_WhenOnline()
    {
        // Arrange
        var processInfo = new ProcessInfo
        {
            ProcessName = "chrome.exe",
            Pid = 1234,
            CpuUsage = 15.5m,
            MemoryUsageMb = 512
        };

        _databaseServiceMock
            .Setup(x => x.CreateProcessSnapshotAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<ProcessInfo>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.CreateProcessSnapshotAsync(100L, 50, processInfo);

        // Assert
        _databaseServiceMock.Verify(
            x => x.CreateProcessSnapshotAsync(100L, 50, processInfo),
            Times.Once);
    }

    [Fact]
    public async Task CreateCpuTemperatureAsync_ShouldCallDatabase_WhenOnline()
    {
        // Arrange
        var temperature = new CpuTemperature
        {
            CpuTctlTdie = 65.5m,
            CpuDieAverage = 62.0m
        };

        _databaseServiceMock
            .Setup(x => x.CreateCpuTemperatureAsync(It.IsAny<long>(), It.IsAny<CpuTemperature>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.CreateCpuTemperatureAsync(100L, temperature);

        // Assert
        _databaseServiceMock.Verify(
            x => x.CreateCpuTemperatureAsync(100L, temperature),
            Times.Once);
    }

    [Fact]
    public async Task TestConnectionAsync_ShouldDelegateToDatabase()
    {
        // Arrange
        _databaseServiceMock
            .Setup(x => x.TestConnectionAsync())
            .Returns(Task.CompletedTask);

        // Act
        await _service.TestConnectionAsync();

        // Assert
        _databaseServiceMock.Verify(x => x.TestConnectionAsync(), Times.Once);
    }

    [Fact]
    public async Task IsConnectionAvailableAsync_ShouldDelegateToDatabase()
    {
        // Arrange
        _databaseServiceMock
            .Setup(x => x.IsConnectionAvailableAsync())
            .ReturnsAsync(true);

        // Act
        var result = await _service.IsConnectionAvailableAsync();

        // Assert
        result.Should().BeTrue();
        _databaseServiceMock.Verify(x => x.IsConnectionAvailableAsync(), Times.Once);
    }

    [Fact]
    public async Task RestoreOfflineSnapshotAsync_ShouldDelegateToDatabase()
    {
        // Arrange
        var snapshotData = new OfflineSnapshotData
        {
            LocalSnapshotId = 1,
            TotalCpuUsage = 25.5m
        };

        _databaseServiceMock
            .Setup(x => x.RestoreOfflineSnapshotAsync(It.IsAny<OfflineSnapshotData>()))
            .ReturnsAsync(999L);

        // Act
        var result = await _service.RestoreOfflineSnapshotAsync(snapshotData);

        // Assert
        result.Should().Be(999L);
        _databaseServiceMock.Verify(
            x => x.RestoreOfflineSnapshotAsync(snapshotData),
            Times.Once);
    }

    [Fact]
    public async Task RestoreOfflineSnapshotBatchAsync_ShouldDelegateToDatabase()
    {
        // Arrange
        var batch = new OfflineSnapshotBatch
        {
            LocalSnapshotId = 1,
            SnapshotData = new OfflineSnapshotData { LocalSnapshotId = 1 }
        };

        _databaseServiceMock
            .Setup(x => x.RestoreOfflineSnapshotBatchAsync(It.IsAny<OfflineSnapshotBatch>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.RestoreOfflineSnapshotBatchAsync(batch);

        // Assert
        _databaseServiceMock.Verify(
            x => x.RestoreOfflineSnapshotBatchAsync(batch),
            Times.Once);
    }

    [Fact]
    public async Task MultipleFailures_ShouldStayInOfflineMode()
    {
        // Arrange
        _databaseServiceMock
            .Setup(x => x.CreateSnapshotAsync(It.IsAny<decimal?>(), It.IsAny<long?>(), It.IsAny<long?>()))
            .ThrowsAsync(new Exception("Database offline"));

        _offlineStorageMock
            .Setup(x => x.GetNextLocalSnapshotId())
            .Returns(() => 1L);

        // Act - Multiple failures
        await _service.CreateSnapshotAsync(10.0m, 1000, 2000);
        await _service.CreateSnapshotAsync(11.0m, 1100, 2100);
        await _service.CreateSnapshotAsync(12.0m, 1200, 2200);

        // Assert - Should have saved 3 snapshots to offline storage
        _offlineStorageMock.Verify(
            x => x.SaveOfflineSnapshotAsync(It.IsAny<OfflineSnapshotBatch>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task OfflineMode_ShouldHandleProcessSnapshots()
    {
        // Arrange - Force offline mode
        _databaseServiceMock
            .Setup(x => x.CreateSnapshotAsync(It.IsAny<decimal?>(), It.IsAny<long?>(), It.IsAny<long?>()))
            .ThrowsAsync(new Exception("Database offline"));

        _offlineStorageMock.Setup(x => x.GetNextLocalSnapshotId()).Returns(1L);
        _offlineStorageMock.Setup(x => x.GetPendingOfflineSnapshotsAsync())
            .ReturnsAsync(new List<OfflineSnapshotBatch>());

        var processInfo = new ProcessInfo
        {
            ProcessName = "chrome.exe",
            ProcessPath = "C:\\Chrome\\chrome.exe",
            Pid = 1234,
            CpuUsage = 15.5m,
            MemoryUsageMb = 512
        };

        // Act - Create snapshot and process snapshot while offline
        var snapshotId = await _service.CreateSnapshotAsync(10.0m, 1000, 2000);
        await _service.CreateProcessSnapshotAsync(snapshotId, 999, processInfo);

        // Assert - Offline storage should be called
        _offlineStorageMock.Verify(
            x => x.SaveOfflineSnapshotAsync(It.IsAny<OfflineSnapshotBatch>()),
            Times.AtLeastOnce);
    }
}
