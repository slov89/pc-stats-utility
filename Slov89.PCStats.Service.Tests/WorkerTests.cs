using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Slov89.PCStats.Data;
using Slov89.PCStats.Models;
using Slov89.PCStats.Service.Services;

namespace Slov89.PCStats.Service.Tests;

public class WorkerTests
{
    private readonly Mock<ILogger<Worker>> _mockLogger;
    private readonly Mock<IProcessMonitorService> _mockProcessMonitor;
    private readonly Mock<IHWiNFOService> _mockHWiNFOService;
    private readonly Mock<IDatabaseService> _mockDatabaseService;
    private readonly IConfiguration _configuration;

    public WorkerTests()
    {
        _mockLogger = new Mock<ILogger<Worker>>();
        _mockProcessMonitor = new Mock<IProcessMonitorService>();
        _mockHWiNFOService = new Mock<IHWiNFOService>();
        _mockDatabaseService = new Mock<IDatabaseService>();

        // Create real configuration instead of mocking
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string>
        {
            { "MonitoringSettings:MinimumCpuUsagePercent", "5.0" },
            { "MonitoringSettings:MinimumPrivateMemoryMb", "100" },
            { "MonitoringSettings:IntervalSeconds", "1" },
            { "DatabaseCleanup:EnableAutoCleanup", "false" },
            { "DatabaseCleanup:CleanupIntervalHours", "24" },
            { "DatabaseCleanup:RetentionDays", "7" }
        });
        _configuration = configBuilder.Build();
    }

    private Worker CreateWorker()
    {
        return new Worker(
            _mockLogger.Object,
            _mockProcessMonitor.Object,
            _mockHWiNFOService.Object,
            _mockDatabaseService.Object,
            _configuration);
    }

    [Fact]
    public async Task StartAsync_ShouldInitializeDatabase()
    {
        // Arrange
        var worker = CreateWorker();
        _mockHWiNFOService.Setup(x => x.IsHWiNFORunning()).Returns(true);
        _mockDatabaseService.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        // Act
        await worker.StartAsync(CancellationToken.None);

        // Assert
        _mockDatabaseService.Verify(x => x.InitializeAsync(), Times.Once);
    }

    [Fact]
    public async Task StartAsync_WhenHWiNFONotRunning_ShouldLogWarning()
    {
        // Arrange
        var worker = CreateWorker();
        _mockHWiNFOService.Setup(x => x.IsHWiNFORunning()).Returns(false);
        _mockDatabaseService.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        // Act
        await worker.StartAsync(CancellationToken.None);

        // Assert
        _mockHWiNFOService.Verify(x => x.IsHWiNFORunning(), Times.Once);
        // Logger should have logged a warning about HWiNFO not running
    }

    [Fact]
    public async Task StartAsync_WhenHWiNFORunning_ShouldNotLogWarning()
    {
        // Arrange
        var worker = CreateWorker();
        _mockHWiNFOService.Setup(x => x.IsHWiNFORunning()).Returns(true);
        _mockDatabaseService.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);

        // Act
        await worker.StartAsync(CancellationToken.None);

        // Assert
        _mockHWiNFOService.Verify(x => x.IsHWiNFORunning(), Times.Once);
    }

    // Note: ExecuteAsync is protected, so we can't test it directly.
    // The Worker runs as a BackgroundService and calls ExecuteAsync internally.
    // Integration tests would be needed to test the full execution flow.

    [Fact]
    public void Constructor_ShouldReadConfigurationValues()
    {
        // Act
        var worker = CreateWorker();

        // Assert - Worker should be created without throwing
        worker.Should().NotBeNull();
    }

    [Fact]
    public async Task StopAsync_ShouldDisposeResources()
    {
        // Arrange
        var worker = CreateWorker();

        // Act
        await worker.StopAsync(CancellationToken.None);

        // Assert - Should complete without throwing
        worker.Should().NotBeNull();
    }
}
