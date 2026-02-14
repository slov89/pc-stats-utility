using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
using PCStats.Models;

namespace PCStats.Data.Tests.Integration;

/// <summary>
/// Integration tests for DatabaseService using a shared PostgreSQL container
/// </summary>
[Collection("PostgreSQL Collection")]
public class DatabaseServiceIntegrationTests : IAsyncLifetime
{
    private readonly SharedPostgreSqlFixture _fixture;
    private readonly DatabaseService _databaseService;

    public DatabaseServiceIntegrationTests(SharedPostgreSqlFixture fixture)
    {
        _fixture = fixture;
        
        // Create service instance with shared connection string
        var mockLogger = new Mock<ILogger<DatabaseService>>();
        _databaseService = new DatabaseService(_fixture.ConnectionString, mockLogger.Object);
    }

    public Task InitializeAsync()
    {
        // No setup needed, using shared fixture
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        // Clean database between tests to ensure isolation
        await _fixture.CleanDatabaseAsync();
    }

    [Fact]
    public async Task TestConnectionAsync_ShouldSucceed()
    {
        // Act & Assert
        await _databaseService.Invoking(s => s.TestConnectionAsync())
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task CreateSnapshotAsync_ShouldReturnValidSnapshotId()
    {
        // Arrange
        var cpuUsage = 45.5m;
        var totalMemory = 16384L;
        var availableMemory = 8192L;

        // Act
        var snapshotId = await _databaseService.CreateSnapshotAsync(cpuUsage, totalMemory, availableMemory);

        // Assert
        snapshotId.Should().BeGreaterThan(0);

        // Verify data was inserted correctly
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT total_cpu_usage, total_memory_usage_mb, total_available_memory_mb FROM snapshots WHERE snapshot_id = @id",
            connection);
        command.Parameters.AddWithValue("id", snapshotId);
        
        await using var reader = await command.ExecuteReaderAsync();
        reader.Read().Should().BeTrue();
        reader.GetDecimal(0).Should().Be(cpuUsage);
        reader.GetInt64(1).Should().Be(totalMemory);
        reader.GetInt64(2).Should().Be(availableMemory);
    }

    [Fact]
    public async Task CreateSnapshotAsync_WithNullValues_ShouldSucceed()
    {
        // Act
        var snapshotId = await _databaseService.CreateSnapshotAsync(null, null, null);

        // Assert
        snapshotId.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetOrCreateProcessAsync_NewProcess_ShouldCreateAndReturnId()
    {
        // Arrange
        var processName = "test.exe";
        var processPath = "C:\\Test\\test.exe";

        // Act
        var processId = await _databaseService.GetOrCreateProcessAsync(processName, processPath);

        // Assert
        processId.Should().BeGreaterThan(0);

        // Verify process was created
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT process_name, process_path FROM processes WHERE process_id = @id",
            connection);
        command.Parameters.AddWithValue("id", processId);
        
        await using var reader = await command.ExecuteReaderAsync();
        reader.Read().Should().BeTrue();
        reader.GetString(0).Should().Be(processName);
        reader.GetString(1).Should().Be(processPath);
    }

    [Fact]
    public async Task GetOrCreateProcessAsync_ExistingProcess_ShouldReturnSameId()
    {
        // Arrange
        var processName = "existing.exe";
        var processPath = "C:\\Existing\\existing.exe";
        var firstId = await _databaseService.GetOrCreateProcessAsync(processName, processPath);

        // Act
        var secondId = await _databaseService.GetOrCreateProcessAsync(processName, processPath);

        // Assert
        secondId.Should().Be(firstId);
    }

    [Fact]
    public async Task CreateProcessSnapshotAsync_ShouldInsertData()
    {
        // Arrange
        var snapshotId = await _databaseService.CreateSnapshotAsync(50m, 16000, 8000);
        var processId = await _databaseService.GetOrCreateProcessAsync("chrome.exe", "C:\\Chrome\\chrome.exe");
        var processInfo = new ProcessInfo
        {
            Pid = 1234,
            ProcessName = "chrome.exe",
            CpuUsage = 25.5m,
            MemoryUsageMb = 512,
            PrivateMemoryMb = 400,
            VirtualMemoryMb = 1024,
            VramUsageMb = 256,
            ThreadCount = 50,
            HandleCount = 300
        };

        // Act
        await _databaseService.CreateProcessSnapshotAsync(snapshotId, processId, processInfo);

        // Assert
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT pid, cpu_usage, memory_usage_mb FROM process_snapshots WHERE snapshot_id = @sid AND process_id = @pid",
            connection);
        command.Parameters.AddWithValue("sid", snapshotId);
        command.Parameters.AddWithValue("pid", processId);
        
        await using var reader = await command.ExecuteReaderAsync();
        reader.Read().Should().BeTrue();
        reader.GetInt32(0).Should().Be(processInfo.Pid);
        reader.GetDecimal(1).Should().Be(processInfo.CpuUsage);
        reader.GetInt64(2).Should().Be(processInfo.MemoryUsageMb);
    }

    [Fact]
    public async Task CreateCpuTemperatureAsync_ShouldInsertData()
    {
        // Arrange
        var snapshotId = await _databaseService.CreateSnapshotAsync(50m, 16000, 8000);
        var temperature = new CpuTemperature
        {
            CpuTctlTdie = 65.5m,
            CpuDieAverage = 62.0m,
            CpuCcd1Tdie = 64.0m,
            CpuCcd2Tdie = 60.0m,
            ThermalLimitPercent = 75.0m,
            ThermalThrottling = false
        };

        // Act
        await _databaseService.CreateCpuTemperatureAsync(snapshotId, temperature);

        // Assert
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT cpu_tctl_tdie, cpu_die_average, thermal_throttling FROM cpu_temperatures WHERE snapshot_id = @sid",
            connection);
        command.Parameters.AddWithValue("sid", snapshotId);
        
        await using var reader = await command.ExecuteReaderAsync();
        reader.Read().Should().BeTrue();
        reader.GetDecimal(0).Should().Be(temperature.CpuTctlTdie.Value);
        reader.GetDecimal(1).Should().Be(temperature.CpuDieAverage.Value);
        reader.GetBoolean(2).Should().Be(temperature.ThermalThrottling.Value);
    }

    [Fact]
    public async Task IsConnectionAvailableAsync_WhenConnected_ShouldReturnTrue()
    {
        // Act
        var result = await _databaseService.IsConnectionAvailableAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task RestoreOfflineSnapshotAsync_ShouldCreateSnapshotAndReturnId()
    {
        // Arrange
        var offlineData = new OfflineSnapshotData
        {
            TotalCpuUsage = 55.5m,
            TotalMemoryMb = 16384,
            AvailableMemoryMb = 7000,
            Timestamp = DateTime.UtcNow,
            LocalSnapshotId = 999
        };

        // Act
        var snapshotId = await _databaseService.RestoreOfflineSnapshotAsync(offlineData);

        // Assert
        snapshotId.Should().BeGreaterThan(0);

        // Verify snapshot was created
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT total_cpu_usage FROM snapshots WHERE snapshot_id = @id",
            connection);
        command.Parameters.AddWithValue("id", snapshotId);
        
        var result = await command.ExecuteScalarAsync();
        result.Should().NotBeNull();
        Convert.ToDecimal(result).Should().Be(offlineData.TotalCpuUsage.Value);
    }

    [Fact]
    public async Task RestoreOfflineSnapshotBatchAsync_ShouldRestoreAllData()
    {
        // Arrange
        var batch = new OfflineSnapshotBatch
        {
            BatchId = Guid.NewGuid(),
            LocalSnapshotId = 100,
            SnapshotData = new OfflineSnapshotData
            {
                TotalCpuUsage = 60m,
                TotalMemoryMb = 16384,
                AvailableMemoryMb = 8000
            },
            ProcessSnapshots = new List<OfflineProcessSnapshotData>
            {
                new()
                {
                    ProcessName = "test1.exe",
                    ProcessPath = "C:\\Test\\test1.exe",
                    ProcessInfo = new ProcessInfo
                    {
                        Pid = 1000,
                        ProcessName = "test1.exe",
                        CpuUsage = 10m,
                        MemoryUsageMb = 200,
                        ThreadCount = 10,
                        HandleCount = 50
                    }
                },
                new()
                {
                    ProcessName = "test2.exe",
                    ProcessPath = "C:\\Test\\test2.exe",
                    ProcessInfo = new ProcessInfo
                    {
                        Pid = 2000,
                        ProcessName = "test2.exe",
                        CpuUsage = 15m,
                        MemoryUsageMb = 300,
                        ThreadCount = 15,
                        HandleCount = 75
                    }
                }
            },
            CpuTemperature = new OfflineCpuTemperatureData
            {
                Temperature = new CpuTemperature
                {
                    CpuTctlTdie = 70m,
                    CpuDieAverage = 68m
                }
            }
        };

        // Act
        await _databaseService.RestoreOfflineSnapshotBatchAsync(batch);

        // Assert - Verify snapshot was created
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        
        await using var snapshotCmd = new NpgsqlCommand(
            "SELECT COUNT(*) FROM snapshots WHERE total_cpu_usage = @cpu",
            connection);
        snapshotCmd.Parameters.AddWithValue("cpu", 60m);
        var snapshotCount = Convert.ToInt32(await snapshotCmd.ExecuteScalarAsync());
        snapshotCount.Should().Be(1);

        // Verify processes were created
        await using var processCmd = new NpgsqlCommand(
            "SELECT COUNT(*) FROM processes WHERE process_name IN ('test1.exe', 'test2.exe')",
            connection);
        var processCount = Convert.ToInt32(await processCmd.ExecuteScalarAsync());
        processCount.Should().Be(2);

        // Verify process snapshots were created
        await using var psCmd = new NpgsqlCommand(
            "SELECT COUNT(*) FROM process_snapshots",
            connection);
        var psCount = Convert.ToInt32(await psCmd.ExecuteScalarAsync());
        psCount.Should().Be(2);

        // Verify CPU temperature was created
        await using var tempCmd = new NpgsqlCommand(
            "SELECT COUNT(*) FROM cpu_temperatures WHERE cpu_tctl_tdie = @temp",
            connection);
        tempCmd.Parameters.AddWithValue("temp", 70m);
        var tempCount = Convert.ToInt32(await tempCmd.ExecuteScalarAsync());
        tempCount.Should().Be(1);
    }

    [Fact]
    public async Task BatchCreateProcessSnapshots_ShouldInsertMultipleRecords()
    {
        // Arrange
        var snapshotId = await _databaseService.CreateSnapshotAsync(50m, 16000, 8000);
        var processId1 = await _databaseService.GetOrCreateProcessAsync("proc1.exe", null);
        var processId2 = await _databaseService.GetOrCreateProcessAsync("proc2.exe", null);

        var snapshots = new List<(int processId, ProcessInfo processInfo)>
        {
            (processId1, new ProcessInfo
            {
                Pid = 100,
                ProcessName = "proc1.exe",
                CpuUsage = 10m,
                MemoryUsageMb = 100,
                ThreadCount = 5,
                HandleCount = 25
            }),
            (processId2, new ProcessInfo
            {
                Pid = 200,
                ProcessName = "proc2.exe",
                CpuUsage = 20m,
                MemoryUsageMb = 200,
                ThreadCount = 10,
                HandleCount = 50
            })
        };

        // Act
        await _databaseService.BatchCreateProcessSnapshotsAsync(snapshotId, snapshots);

        // Assert
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT COUNT(*) FROM process_snapshots WHERE snapshot_id = @sid",
            connection);
        command.Parameters.AddWithValue("sid", snapshotId);
        
        var count = Convert.ToInt32(await command.ExecuteScalarAsync());
        count.Should().Be(2);
    }

    [Fact]
    public async Task CascadeDelete_DeletingSnapshot_ShouldDeleteRelatedRecords()
    {
        // Arrange
        var snapshotId = await _databaseService.CreateSnapshotAsync(50m, 16000, 8000);
        var processId = await _databaseService.GetOrCreateProcessAsync("temp.exe", null);
        await _databaseService.CreateProcessSnapshotAsync(snapshotId, processId, new ProcessInfo
        {
            Pid = 999,
            ProcessName = "temp.exe",
            CpuUsage = 5m,
            MemoryUsageMb = 50,
            ThreadCount = 1,
            HandleCount = 10
        });

        // Act - Delete snapshot
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using var deleteCmd = new NpgsqlCommand(
            "DELETE FROM snapshots WHERE snapshot_id = @id",
            connection);
        deleteCmd.Parameters.AddWithValue("id", snapshotId);
        await deleteCmd.ExecuteNonQueryAsync();

        // Assert - Verify process_snapshot was also deleted (cascade)
        await using var checkCmd = new NpgsqlCommand(
            "SELECT COUNT(*) FROM process_snapshots WHERE snapshot_id = @id",
            connection);
        checkCmd.Parameters.AddWithValue("id", snapshotId);
        var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
        count.Should().Be(0);
    }
}

