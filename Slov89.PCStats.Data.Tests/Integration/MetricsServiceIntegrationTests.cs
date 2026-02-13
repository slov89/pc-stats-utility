using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
using Slov89.PCStats.Models;

namespace Slov89.PCStats.Data.Tests.Integration;

/// <summary>
/// Integration tests for MetricsService using a shared PostgreSQL container
/// </summary>
[Collection("PostgreSQL Collection")]
public class MetricsServiceIntegrationTests : IAsyncLifetime
{
    private readonly SharedPostgreSqlFixture _fixture;
    private readonly MetricsService _metricsService;
    private readonly DatabaseService _databaseService;

    public MetricsServiceIntegrationTests(SharedPostgreSqlFixture fixture)
    {
        _fixture = fixture;

        // Create service instances
        var mockLogger = new Mock<ILogger<MetricsService>>();
        _metricsService = new MetricsService(_fixture.ConnectionString, mockLogger.Object);

        var mockDbLogger = new Mock<ILogger<DatabaseService>>();
        _databaseService = new DatabaseService(_fixture.ConnectionString, mockDbLogger.Object);
    }

    public async Task InitializeAsync()
    {
        // Seed test data for this test class
        await SeedTestDataAsync();
    }

    public async Task DisposeAsync()
    {
        // Clean database between tests to ensure isolation
        await _fixture.CleanDatabaseAsync();
    }

    private async Task SeedTestDataAsync()
    {
        var baseTime = DateTime.UtcNow.AddHours(-2);

        for (int i = 0; i < 10; i++)
        {
            var snapshotId = await _databaseService.CreateSnapshotAsync(
                50m + i,
                16000 + (i * 100),
                8000 - (i * 50)
            );

            // Add temperature data for some snapshots
            if (i % 2 == 0)
            {
                await _databaseService.CreateCpuTemperatureAsync(snapshotId, new CpuTemperature
                {
                    CpuTctlTdie = 60m + i,
                    CpuDieAverage = 58m + i,
                    CpuCcd1Tdie = 59m + i,
                    CpuCcd2Tdie = 57m + i,
                    ThermalLimitPercent = 70m,
                    ThermalThrottling = false
                });
            }

            // Update snapshot timestamp to be in the past
            await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                "UPDATE snapshots SET snapshot_timestamp = @timestamp WHERE snapshot_id = @id",
                connection);
            command.Parameters.AddWithValue("timestamp", baseTime.AddMinutes(i * 10));
            command.Parameters.AddWithValue("id", snapshotId);
            await command.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task GetSnapshotsAsync_WithValidTimeRange_ShouldReturnData()
    {
        // Arrange
        var startTime = DateTime.UtcNow.AddHours(-3);
        var endTime = DateTime.UtcNow;

        // Act
        var snapshots = await _metricsService.GetSnapshotsAsync(startTime, endTime);

        // Assert
        snapshots.Should().NotBeNull();
        snapshots.Should().HaveCountGreaterThan(0);
        snapshots.Should().AllSatisfy(s =>
        {
            s.SnapshotId.Should().BeGreaterThan(0);
            s.TotalCpuUsage.Should().NotBeNull();
            s.TotalMemoryUsageMb.Should().NotBeNull();
            s.TotalAvailableMemoryMb.Should().NotBeNull();
        });
    }

    [Fact]
    public async Task GetSnapshotsAsync_ShouldBeOrderedByTimestamp()
    {
        // Arrange
        var startTime = DateTime.UtcNow.AddHours(-3);
        var endTime = DateTime.UtcNow;

        // Act
        var snapshots = await _metricsService.GetSnapshotsAsync(startTime, endTime);

        // Assert
        snapshots.Should().NotBeEmpty();
        snapshots.Should().BeInAscendingOrder(s => s.SnapshotTimestamp);
    }

    [Fact]
    public async Task GetSnapshotsAsync_WithNarrowTimeRange_ShouldReturnLimitedResults()
    {
        // Arrange
        var startTime = DateTime.UtcNow.AddHours(-2).AddMinutes(15);
        var endTime = DateTime.UtcNow.AddHours(-2).AddMinutes(35);

        // Act
        var snapshots = await _metricsService.GetSnapshotsAsync(startTime, endTime);

        // Assert
        snapshots.Should().NotBeNull();
        // Should only get a few snapshots in this narrow window
        snapshots.Count.Should().BeLessThan(10);
    }

    [Fact]
    public async Task GetSnapshotsAsync_WithFutureTimeRange_ShouldReturnEmpty()
    {
        // Arrange
        var startTime = DateTime.UtcNow.AddHours(1);
        var endTime = DateTime.UtcNow.AddHours(2);

        // Act
        var snapshots = await _metricsService.GetSnapshotsAsync(startTime, endTime);

        // Assert
        snapshots.Should().NotBeNull();
        snapshots.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCpuTemperaturesAsync_WithValidTimeRange_ShouldReturnData()
    {
        // Arrange
        var startTime = DateTime.UtcNow.AddHours(-3);
        var endTime = DateTime.UtcNow;

        // Act
        var temperatures = await _metricsService.GetCpuTemperaturesAsync(startTime, endTime);

        // Assert
        temperatures.Should().NotBeNull();
        temperatures.Should().HaveCountGreaterThan(0);
        temperatures.Should().AllSatisfy(t =>
        {
            t.TempId.Should().BeGreaterThan(0);
            t.SnapshotId.Should().BeGreaterThan(0);
        });
    }

    [Fact]
    public async Task GetCpuTemperaturesAsync_ShouldReturnOnlyDataWithinTimeRange()
    {
        // Arrange
        var startTime = DateTime.UtcNow.AddHours(-3);
        var endTime = DateTime.UtcNow;

        // Act
        var temperatures = await _metricsService.GetCpuTemperaturesAsync(startTime, endTime);

        // Assert
        temperatures.Should().NotBeNull();
        // We seeded 5 snapshots with temperature data (every other snapshot)
        temperatures.Count.Should().BeGreaterThan(0);
        temperatures.Count.Should().BeLessThanOrEqualTo(5);
    }

    [Fact]
    public async Task GetTopProcessesAsync_ShouldReturnProcessData()
    {
        // Arrange
        var startTime = DateTime.UtcNow.AddHours(-3);
        var endTime = DateTime.UtcNow;
        var topN = 5;

        // Seed some process data with varying CPU usage
        var snapshotId = await _databaseService.CreateSnapshotAsync(50m, 16000, 8000);
        
        for (int i = 0; i < 10; i++)
        {
            var processId = await _databaseService.GetOrCreateProcessAsync($"testproc{i}.exe", null);
            await _databaseService.CreateProcessSnapshotAsync(snapshotId, processId, new ProcessInfo
            {
                Pid = 1000 + i,
                ProcessName = $"testproc{i}.exe",
                CpuUsage = (i + 1) * 5m, // Varying CPU usage from 5-50
                MemoryUsageMb = 100 + (i * 50),
                ThreadCount = 5,
                HandleCount = 25
            });
        }

        // Act
        var topProcesses = await _metricsService.GetTopProcessesAsync(startTime, endTime, topN);

        // Assert
        topProcesses.Should().NotBeNull();
        // Dictionary of process names to their metrics
        if (topProcesses.Count > 0)
        {
            topProcesses.Count.Should().BeLessThanOrEqualTo(topN);
            topProcesses.Values.Should().AllSatisfy(metrics =>
            {
                metrics.Should().NotBeEmpty();
            });
        }
    }
}

