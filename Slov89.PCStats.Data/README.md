# Slov89.PCStats.Data

Data access layer providing database operations and metrics queries for the PC Stats Monitoring solution.

## Overview

This class library encapsulates all PostgreSQL database interactions, providing:
- **Write operations** for storing monitoring data (used by Service)
- **Read operations** for querying metrics (used by Dashboard)
- **Abstraction layer** separating database logic from business logic

## Services

### DatabaseService

Handles write operations for storing monitoring data.

**Interface:** `IDatabaseService`

**Methods:**
- `TestConnectionAsync()` - Test database connectivity
- `InitializeAsync()` - Initialize database connection
- `CreateSnapshotAsync(totalCpuUsage, totalMemoryMb, availableMemoryMb)` - Create system snapshot, returns snapshot ID
- `GetOrCreateProcessAsync(processName, processPath)` - Get existing or create new process record, returns process ID
- `CreateProcessSnapshotAsync(snapshotId, processId, processInfo)` - Create process performance snapshot
- `CreateCpuTemperatureAsync(snapshotId, temperature)` - Create CPU temperature record
- `BatchCreateProcessSnapshotsAsync(snapshotId, processSnapshots)` - Batch insert process snapshots (transactional)
- `CleanupOldSnapshotsAsync(daysToKeep)` - Remove snapshots older than specified days, returns count deleted

**Offline Storage Methods:**
- `IsConnectionAvailableAsync()` - Check if database connection is available
- `RestoreOfflineSnapshotAsync(snapshotData)` - Restore snapshot from offline storage
- `RestoreOfflineProcessAsync(processData)` - Restore process from offline storage
- `RestoreOfflineSnapshotBatchAsync(batch)` - Restore entire snapshot batch (transactional)

**Configuration:**
- Reads connection string from `slov89_pc_stats_utility_pg` environment variable
- Requires `IConfiguration` and `ILogger<DatabaseService>` via dependency injection

**Usage Example:**
```csharp
// Create snapshot
var snapshotId = await _databaseService.CreateSnapshotAsync(
    totalCpuUsage: 45.2m,
    totalMemoryMb: 16384,
    availableMemoryMb: 8192
);

// Get/create process
var processId = await _databaseService.GetOrCreateProcessAsync(
    processName: "chrome.exe",
    processPath: "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe"
);

// Create process snapshot
await _databaseService.CreateProcessSnapshotAsync(snapshotId, processId, processInfo);

// Cleanup old data
var deletedCount = await _databaseService.CleanupOldSnapshotsAsync(daysToKeep: 7);
```

### MetricsService

Handles read operations for querying time-series metrics.

**Interface:** `IMetricsService`

**Methods:**
- `GetSnapshotsAsync(startTime, endTime)` - Get system snapshots within time range
- `GetCpuTemperaturesAsync(startTime, endTime)` - Get CPU temperature readings within time range
- `GetTopProcessesAsync(startTime, endTime, topCount)` - Get top N processes by average CPU usage with time-series data

**Configuration:**
- Reads connection string from `slov89_pc_stats_utility_pg` environment variable
- Requires `ILogger<MetricsService>` via dependency injection

**Usage Example:**
```csharp
var endTime = DateTime.Now;
var startTime = endTime.AddMinutes(-10);

// Get system snapshots
var snapshots = await _metricsService.GetSnapshotsAsync(startTime, endTime);

// Get CPU temperatures
var temperatures = await _metricsService.GetCpuTemperaturesAsync(startTime, endTime);

// Get top 5 processes
var topProcesses = await _metricsService.GetTopProcessesAsync(startTime, endTime, 5);
```

## Offline Storage Support

The `DatabaseService` includes methods for restoring data from offline JSON storage when the database becomes available again.

**Offline Recovery Flow:**
1. Service detects database is offline
2. Data saved to JSON files by `OfflineStorageService`
3. When database reconnects, `OfflineDatabaseService` triggers recovery
4. `RestoreOfflineSnapshotBatchAsync()` restores each batch transactionally
5. Successfully restored batches are removed from offline storage

**Key Features:**
- Maintains referential integrity during restoration
- Uses database transactions for atomicity
- Preserves original timestamps from offline storage
- Handles partial failures gracefully

## Environment Variables

**Required:**
- `slov89_pc_stats_utility_pg` - PostgreSQL connection string
  - Format: `Host=localhost;Port=5432;Database=pcstats;Username=postgres;Password=your_password`
  - Set using the `Set-ConnectionString.ps1` script in the root directory

## Dependencies

### NuGet Packages
- `Npgsql` (10.0.1) - PostgreSQL driver for .NET
- `Microsoft.Extensions.Configuration.Abstractions` (10.0.3) - Configuration support
- `Microsoft.Extensions.Logging.Abstractions` (10.0.3) - Logging support

### Project References
- `Slov89.PCStats.Models` - Data models

## Database Schema

This library assumes the following PostgreSQL schema exists:
- `snapshots` - System snapshots
- `processes` - Process definitions
- `process_snapshots` - Process performance metrics
- `cpu_temperatures` - CPU temperature readings

See [Database/README.md](../Database/README.md) for full schema details.

## Dependency Injection Setup

### Service (Write Operations)
```csharp
builder.Services.AddSingleton<IDatabaseService, DatabaseService>();
```

### Dashboard (Read Operations)
```csharp
builder.Services.AddScoped<IMetricsService, MetricsService>();
```

## Error Handling

Both services throw exceptions on database errors:
- `InvalidOperationException` - Environment variable not set
- `NpgsqlException` - Database connection/query errors

All exceptions are logged via `ILogger`.

## Testing

This project has comprehensive test coverage in **Slov89.PCStats.Data.Tests**.

### Integration Tests

**DatabaseServiceIntegrationTests:**
- Tests all write operations (snapshots, processes, temperatures)
- Validates transactional integrity
- Tests offline data restoration
- Verifies foreign key relationships

**MetricsServiceIntegrationTests:**
- Tests time-range queries for snapshots and temperatures
- Validates top processes query with aggregations
- Tests query performance and accuracy

**Setup:**
- Uses shared PostgreSQL test fixture
- Creates temporary test database for each test run
- Cleans up after tests complete

### Model Tests

**OfflineDataModelsTests:**
- Validates offline storage data structures
- Tests serialization/deserialization
- Ensures data integrity during offline mode

### Running Tests

```powershell
# Run all data layer tests
dotnet test Slov89.PCStats.Data.Tests

# Run only integration tests
dotnet test Slov89.PCStats.Data.Tests --filter Category=Integration

# Run with detailed output
dotnet test Slov89.PCStats.Data.Tests --logger "console;verbosity=detailed"
```

**Requirements:**
- PostgreSQL 12+ must be running
- Connection string configured via environment variable or test settings
- Write permissions to create/drop test databases

## Related Documentation

- **[Main README](../README.md)** - Solution overview and setup guide
- **[Models Documentation](../Slov89.PCStats.Models/README.md)** - Data models used by this layer
- **[Database Schema](../Database/README.md)** - PostgreSQL schema and queries
- **[Service Documentation](../Slov89.PCStats.Service/README.md)** - Uses DatabaseService for writes
- **[Dashboard Documentation](../Slov89.PCStats.Dashboard/README.md)** - Uses MetricsService for reads

## Target Framework

- .NET 10.0
