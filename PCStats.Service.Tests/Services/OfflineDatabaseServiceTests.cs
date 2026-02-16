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

    [Fact]
    public async Task CreateSnapshotWithDataAsync_ShouldCallDatabase_WhenOnline()
    {
        // Arrange
        var processSnapshots = new List<(int processId, ProcessInfo processInfo)>
        {
            (123, new ProcessInfo { ProcessName = "chrome.exe", Pid = 1234, CpuUsage = 15.5m, MemoryUsageMb = 512 }),
            (456, new ProcessInfo { ProcessName = "firefox.exe", Pid = 5678, CpuUsage = 10.2m, MemoryUsageMb = 384 })
        };

        var temperature = new CpuTemperature { CpuTctlTdie = 65.5m, CpuDieAverage = 62.0m };

        _databaseServiceMock
            .Setup(x => x.CreateSnapshotWithDataAsync(
                It.IsAny<decimal?>(), It.IsAny<long?>(), It.IsAny<long?>(),
                It.IsAny<List<(int, ProcessInfo)>>(), It.IsAny<CpuTemperature?>()))
            .ReturnsAsync(999L);

        // Act
        var result = await _service.CreateSnapshotWithDataAsync(25.5m, 8192, 4096, processSnapshots, temperature);

        // Assert
        result.Should().Be(999L);
        _databaseServiceMock.Verify(
            x => x.CreateSnapshotWithDataAsync(25.5m, 8192, 4096, processSnapshots, temperature),
            Times.Once);
        _offlineStorageMock.Verify(
            x => x.SaveOfflineSnapshotAsync(It.IsAny<OfflineSnapshotBatch>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateSnapshotWithDataAsync_ShouldUseOfflineStorage_WhenAlreadyInOfflineMode()
    {
        // Arrange - Force offline mode by failing batch process creation first
        _databaseServiceMock
            .Setup(x => x.BatchGetOrCreateProcessesAsync(It.IsAny<List<(string, string?)>>()))
            .ThrowsAsync(new Exception("Database offline"));

        _offlineStorageMock.Setup(x => x.GetNextLocalSnapshotId()).Returns(999L);
        _offlineStorageMock.Setup(x => x.GetPendingOfflineSnapshotsAsync())
            .ReturnsAsync(new List<OfflineSnapshotBatch>());

        // Force offline mode by failing batch process creation
        var processes = new List<(string, string?)> { ("chrome.exe", null), ("firefox.exe", null) };
        var fakeProcessIds = await _service.BatchGetOrCreateProcessesAsync(processes);

        var processSnapshots = new List<(int, ProcessInfo)>
        {
            (fakeProcessIds["chrome.exe|"], new ProcessInfo { ProcessName = "chrome.exe", Pid = 1234, CpuUsage = 15.5m, MemoryUsageMb = 512 }),
            (fakeProcessIds["firefox.exe|"], new ProcessInfo { ProcessName = "firefox.exe", Pid = 5678, CpuUsage = 10.2m, MemoryUsageMb = 384 })
        };

        var temperature = new CpuTemperature { CpuTctlTdie = 65.5m };

        // Act - Call CreateSnapshotWithDataAsync while already in offline mode (THIS WAS THE BUG!)
        var snapshotId = await _service.CreateSnapshotWithDataAsync(25.5m, 8192, 4096, processSnapshots, temperature);

        // Assert - Should NOT attempt to call database with fake offline process IDs
        _databaseServiceMock.Verify(
            x => x.CreateSnapshotWithDataAsync(
                It.IsAny<decimal?>(), It.IsAny<long?>(), It.IsAny<long?>(),
                It.IsAny<List<(int, ProcessInfo)>>(), It.IsAny<CpuTemperature?>()),
            Times.Never); // This would have caught the FK constraint bug!

        // Should save to offline storage instead
        _offlineStorageMock.Verify(
            x => x.SaveOfflineSnapshotAsync(It.Is<OfflineSnapshotBatch>(
                batch => batch.ProcessSnapshots.Count == 2 &&
                         batch.CpuTemperature != null)),
            Times.AtLeastOnce);

        snapshotId.Should().Be(999L);
    }

    [Fact]
    public async Task CreateSnapshotWithDataAsync_ShouldSwitchToOfflineMode_WhenDatabaseFails()
    {
        // Arrange
        var processSnapshots = new List<(int, ProcessInfo)>
        {
            (123, new ProcessInfo { ProcessName = "chrome.exe", Pid = 1234, CpuUsage = 15.5m, MemoryUsageMb = 512 })
        };

        _databaseServiceMock
            .Setup(x => x.CreateSnapshotWithDataAsync(
                It.IsAny<decimal?>(), It.IsAny<long?>(), It.IsAny<long?>(),
                It.IsAny<List<(int, ProcessInfo)>>(), It.IsAny<CpuTemperature?>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        _offlineStorageMock.Setup(x => x.GetNextLocalSnapshotId()).Returns(777L);

        // Act
        var result = await _service.CreateSnapshotWithDataAsync(25.5m, 8192, 4096, processSnapshots, null);

        // Assert - Should have switched to offline mode
        result.Should().Be(777L);
        _offlineStorageMock.Verify(
            x => x.SaveOfflineSnapshotAsync(It.Is<OfflineSnapshotBatch>(
                batch => batch.SnapshotData != null &&
                         batch.ProcessSnapshots.Count == 1)),
            Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_ShouldConnectToDatabase_WhenAvailable()
    {
        // Arrange
        _databaseServiceMock
            .Setup(x => x.InitializeAsync())
            .Returns(Task.CompletedTask);

        _offlineStorageMock
            .Setup(x => x.IsRecoveryNeededAsync())
            .ReturnsAsync(false);

        // Act
        await _service.InitializeAsync();

        // Assert
        _databaseServiceMock.Verify(x => x.InitializeAsync(), Times.Once);
        _offlineStorageMock.Verify(x => x.IsRecoveryNeededAsync(), Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_ShouldRecoverPendingOfflineData_WhenDatabaseIsOnline()
    {
        // Arrange
        _databaseServiceMock
            .Setup(x => x.InitializeAsync())
            .Returns(Task.CompletedTask);

        _offlineStorageMock
            .Setup(x => x.IsRecoveryNeededAsync())
            .ReturnsAsync(true);

        _offlineStorageMock
            .Setup(x => x.GetPendingSnapshotCountAsync())
            .ReturnsAsync(50);

        // Act
        await _service.InitializeAsync();

        // Allow background recovery task to start
        await Task.Delay(100);

        // Assert - Should check for and report pending offline data
        _offlineStorageMock.Verify(x => x.IsRecoveryNeededAsync(), Times.Once);
        _offlineStorageMock.Verify(x => x.GetPendingSnapshotCountAsync(), Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_ShouldStartInOfflineMode_WhenDatabaseUnavailable()
    {
        // Arrange
        _databaseServiceMock
            .Setup(x => x.InitializeAsync())
            .ThrowsAsync(new Exception("Database connection failed"));

        _offlineStorageMock.Setup(x => x.GetNextLocalSnapshotId()).Returns(1L);

        // Act
        await _service.InitializeAsync();

        // Verify we're in offline mode by testing subsequent operations
        var result = await _service.GetOrCreateProcessAsync("test.exe", null);

        // Assert - Should return hashed ID (indicates offline mode)
        result.Should().BeGreaterThan(0);
        _databaseServiceMock.Verify(x => x.GetOrCreateProcessAsync(It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task BatchGetOrCreateProcessesAsync_FollowedByCreateSnapshotWithDataAsync_ShouldNotCauseForeignKeyViolation()
    {
        // Arrange - Simulate the exact bug scenario
        // Step 1: BatchGetOrCreateProcessesAsync fails and switches to offline mode
        _databaseServiceMock
            .Setup(x => x.BatchGetOrCreateProcessesAsync(It.IsAny<List<(string, string?)>>()))
            .ThrowsAsync(new Exception("Database connection lost"));

        _offlineStorageMock.Setup(x => x.GetNextLocalSnapshotId()).Returns(111L);
        _offlineStorageMock.Setup(x => x.GetPendingOfflineSnapshotsAsync())
            .ReturnsAsync(new List<OfflineSnapshotBatch>());

        // Act - This is the real-world call sequence that caused the bug
        var processes = new List<(string, string?)>
        {
            ("chrome.exe", "C:\\Chrome\\chrome.exe"),
            ("firefox.exe", "C:\\Firefox\\firefox.exe")
        };

        // Step 1: Get fake process IDs (switches to offline mode)
        var processIdMap = await _service.BatchGetOrCreateProcessesAsync(processes);

        // Step 2: Create snapshot with those fake IDs
        var processSnapshots = new List<(int, ProcessInfo)>
        {
            (processIdMap["chrome.exe|C:\\Chrome\\chrome.exe"],
                new ProcessInfo { ProcessName = "chrome.exe", ProcessPath = "C:\\Chrome\\chrome.exe", Pid = 1234, CpuUsage = 15m, MemoryUsageMb = 500 }),
            (processIdMap["firefox.exe|C:\\Firefox\\firefox.exe"],
                new ProcessInfo { ProcessName = "firefox.exe", ProcessPath = "C:\\Firefox\\firefox.exe", Pid = 5678, CpuUsage = 10m, MemoryUsageMb = 400 })
        };

        var temperature = new CpuTemperature { CpuTctlTdie = 65m };

        var snapshotId = await _service.CreateSnapshotWithDataAsync(25m, 8000, 4000, processSnapshots, temperature);

        // Assert - The critical fix: Should NOT try to use fake IDs with real database
        _databaseServiceMock.Verify(
            x => x.CreateSnapshotWithDataAsync(
                It.IsAny<decimal?>(), It.IsAny<long?>(), It.IsAny<long?>(),
                It.IsAny<List<(int, ProcessInfo)>>(), It.IsAny<CpuTemperature?>()),
            Times.Never,
            "CreateSnapshotWithDataAsync should not attempt database operation when already in offline mode");

        // Should save everything to offline storage
        _offlineStorageMock.Verify(
            x => x.SaveOfflineSnapshotAsync(It.Is<OfflineSnapshotBatch>(
                batch => batch.ProcessSnapshots.Count == 2 &&
                         batch.CpuTemperature != null &&
                         batch.SnapshotData != null)),
            Times.Once);

        snapshotId.Should().Be(111L);
    }
}
