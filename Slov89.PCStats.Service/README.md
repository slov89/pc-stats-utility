# Slov89.PCStats.Service

Windows service that monitors system performance and logs metrics to PostgreSQL at configurable intervals (default: 5 seconds).

## Overview

This background service continuously collects:
- System-level metrics (CPU, memory)
- Per-process metrics (CPU, memory, VRAM, threads, handles)
- CPU temperature data (via HWiNFO integration)

Data is stored in PostgreSQL for analysis and visualization.

## Features

- **Automatic Collection**: Configurable interval (default: every 5 seconds)
- **Smart Filtering**: Only saves detailed metrics for processes with CPU >= 1% OR memory >= 100MB
- **Process Tracking**: All processes tracked in database, regardless of threshold
- **Automatic Cleanup**: Periodic removal of old data based on retention period (default: 7 days)
- **HWiNFO Integration**: Optional CPU temperature monitoring
- **Offline Storage**: Automatic fallback to local JSON storage when database is unavailable
- **Automatic Recovery**: Bulk restoration of offline data when database reconnects
- **Zero Data Loss**: Continuous monitoring even during database outages
- **Windows Service**: Runs in background, starts with Windows
- **Configurable**: All settings in `appsettings.json`

## Architecture

### Services

#### ProcessMonitorService
Collects process information and performance metrics.

**Methods:**
- `GetRunningProcessesAsync()` - Enumerate all running processes
- `GetSystemCpuUsageAsync()` - Get total system CPU usage
- Internal: CPU usage calculation, VRAM detection, performance counter reading

#### HWiNFOService
Reads CPU temperature data from HWiNFO shared memory.

**Methods:**
- `GetCpuTemperaturesAsync()` - Read current CPU temperatures
- `IsHWiNFORunning()` - Check if HWiNFO is running
- Falls back to registry if shared memory unavailable

#### OfflineStorageService
Manages offline data storage when database is unavailable.

**Methods:**
- `SaveOfflineSnapshotAsync()` - Save snapshot batch to JSON file
- `GetPendingOfflineSnapshotsAsync()` - Retrieve all pending offline snapshots
- `RemoveOfflineSnapshotAsync()` - Delete successfully restored snapshot
- `GetNextLocalSnapshotId()` - Generate local snapshot IDs for offline mode
- `CleanupOldOfflineDataAsync()` - Remove old offline files

#### OfflineDatabaseService
Wrapper around DatabaseService that adds offline storage capabilities.

**Features:**
- Detects database connection failures
- Automatically switches to offline mode
- Queues data in local JSON files
- Monitors for database recovery
- Bulk restores offline data when connection returns
- Handles retry logic and error management

#### Worker
Main background service that orchestrates data collection.

**Flow:**
1. Collect system metrics (CPU, memory)
2. Collect all running processes
3. Read CPU temperatures (if HWiNFO running)
4. Create snapshot in database (or offline storage)
5. Track all processes in `processes` table
6. Filter processes by dual threshold (CPU >= 1% OR Memory >= 100MB)
7. Save detailed metrics for filtered processes
8. Periodically run automatic cleanup (every 24 hours by default)
9. Wait for configured interval, repeat

## Configuration

### Environment Variable (Required)

Set the PostgreSQL connection string:

```powershell
.\Set-ConnectionString.ps1 -ConnectionString "Host=localhost;Port=5432;Database=pcstats;Username=postgres;Password=YOUR_PASSWORD"
```

This sets the `slov89_pc_stats_utility_pg` environment variable.

### appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.Hosting.Lifetime": "Information",
      "Slov89.PCStats.Service": "Warning",
      "Slov89.PCStats.Service.Services.OfflineStorageService": "Information",
      "Slov89.PCStats.Service.Services.OfflineDatabaseService": "Information"
    }
  },
  "MonitoringSettings": {
    "IntervalSeconds": 5,
    "EnableVRAMMonitoring": true,
    "EnableCPUTemperatureMonitoring": true,
    "MinimumCpuUsagePercent": 1.0,
    "MinimumPrivateMemoryMb": 100
  },
  "DatabaseCleanup": {
    "EnableAutoCleanup": true,
    "CleanupIntervalHours": 24,
    "RetentionDays": 7
  },
  "OfflineStorage": {
    "Path": "C:\\\\ProgramData\\\\Slov89.PCStats.Service\\\\OfflineData",
    "MaxRetentionDays": 7,
    "EnableOfflineMode": true
  }
}
```

**Monitoring Settings:**
- `IntervalSeconds` - Collection interval in seconds (default: 5)
- `EnableVRAMMonitoring` - Enable VRAM tracking (may not work on all GPUs)
- `EnableCPUTemperatureMonitoring` - Enable HWiNFO temperature monitoring
- `MinimumCpuUsagePercent` - CPU threshold for detailed metrics (default: 1.0%, 0 = all processes)
- `MinimumPrivateMemoryMb` - Memory threshold for detailed metrics (default: 100MB)

**Database Cleanup Settings:**
- `EnableAutoCleanup` - Enable automatic database cleanup (default: true)
- `CleanupIntervalHours` - How often to run cleanup in hours (default: 24)
- `RetentionDays` - How many days of data to keep (default: 7)

**Offline Storage Settings:**
- `Path` - Directory for offline JSON files (default: `C:\\ProgramData\\Slov89.PCStats.Service\\OfflineData`)
- `MaxRetentionDays` - How long to keep offline files before cleanup (default: 7 days)
- `EnableOfflineMode` - Enable offline storage fallback (default: true)

## Offline Storage

### How It Works

When the database connection is lost, the service automatically:

1. **Detects Failure**: Database operations throw exceptions
2. **Switches to Offline Mode**: All data saved to local JSON files
3. **Continues Monitoring**: No interruption to data collection
4. **Monitors for Recovery**: Checks connection on each operation
5. **Restores Data**: When connection returns, bulk restores all offline data
6. **Cleans Up**: Removes successfully restored files

### Storage Location

Default: `C:\\ProgramData\\Slov89.PCStats.Service\\OfflineData\\`

Files are named: `snapshot_{BatchId}{Timestamp}.json`

### Data Retention

- Offline files are automatically cleaned up after `MaxRetentionDays` (default: 7 days)
- Successfully restored files are deleted immediately
- Failed batches retry up to 3 times before being discarded

### Recovery Process

The service automatically:
- Restores snapshots in chronological order
- Maintains all relationships (processes, temperatures)
- Uses database transactions for data integrity
- Logs detailed recovery progress
- Handles partial failures gracefully

## Installation

### Prerequisites
- .NET 10 Runtime
- PostgreSQL 12+ with initialized schema (see [Database/README.md](../Database/README.md))
- HWiNFO v8.14 (optional, for temperature monitoring)

### Option 1: Using Install Script (Recommended)

Open PowerShell as Administrator:

```powershell
cd Slov89.PCStats.Service
.\Install-Service.ps1
```

The script will:
- Build the project
- Publish to `C:\Services\Slov89.PCStats.Service`
- Create Windows service
- Prompt to start the service

### Option 2: Manual Installation

```powershell
# Build and publish
dotnet publish -c Release -o C:\Services\Slov89.PCStats.Service

# Install as Windows Service (requires Administrator)
sc.exe create "Slov89.PCStats.Service" binPath="C:\Services\Slov89.PCStats.Service\Slov89.PCStats.Service.exe" start=auto
sc.exe description "Slov89.PCStats.Service" "Monitors PC performance stats and logs to PostgreSQL database"
sc.exe start "Slov89.PCStats.Service"
```

## HWiNFO Setup (Optional)

For CPU temperature monitoring:

1. Install [HWiNFO v8.14](https://www.hwinfo.com/)
2. Open HWiNFO Settings (gear icon)
3. Enable "Shared Memory Support"
4. Click OK and restart HWiNFO
5. Keep HWiNFO running in background

**Note:** Service runs without HWiNFO, it just won't collect temperature data.

**Troubleshooting HWiNFO:**

If temperature data is not being collected, use the HWiNFODiagnostics utility to verify sensor access:

```powershell
cd HWiNFODiagnostics
dotnet run
```

This will show all available sensors and verify shared memory access. See [HWiNFODiagnostics/README.md](../HWiNFODiagnostics/README.md) for detailed usage and troubleshooting.

## Service Management

### Start/Stop

```powershell
# Start
Start-Service "Slov89.PCStats.Service"

# Stop
Stop-Service "Slov89.PCStats.Service"

# Status
Get-Service "Slov89.PCStats.Service"
```

### View Logs

1. Open Event Viewer (`eventvwr.msc`)
2. Navigate to: Windows Logs → Application
3. Filter by source: "Slov89.PCStats.Service"

**Log Levels:**
- **Information**: Service start/stop, snapshots created
- **Warning**: HWiNFO not running, connection issues
- **Error**: Database errors, critical failures

### Uninstall

```powershell
# Using script
cd Slov89.PCStats.Service
.\Uninstall-Service.ps1

# Or manually
Stop-Service "Slov89.PCStats.Service"
sc.exe delete "Slov89.PCStats.Service"
```

## Development

### Run in Console Mode

```powershell
cd Slov89.PCStats.Service
dotnet run
```

Press Ctrl+C to stop.

### Debug Logging

Edit `appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Slov89.PCStats.Service": "Debug"
    }
  }
}
```

Then run:
```powershell
dotnet run --environment Development
```

## Dependencies

### NuGet Packages
- `Npgsql` (10.0.1) - PostgreSQL driver
- `Microsoft.Extensions.Hosting.WindowsServices` - Windows service hosting
- `System.Diagnostics.PerformanceCounter` - Performance counters
- `System.Management` - WMI for VRAM

### Project References
- `Slov89.PCStats.Data` - Database operations
- `Slov89.PCStats.Models` - Data models (via Data)

## Troubleshooting

### Service Won't Start
- Check Event Viewer for detailed errors
- Verify PostgreSQL is running: `Get-Service postgresql*`
- Verify environment variable is set: `[Environment]::GetEnvironmentVariable('slov89_pc_stats_utility_pg', 'Machine')`
- Check database schema exists

### No Temperature Data
- Ensure HWiNFO v8.14 is running
- Verify "Shared Memory Support" is enabled in HWiNFO
- Service works without HWiNFO (optional feature)

### High CPU Usage
- Normal usage: 1-3% on average
- Check process count (200+ processes = higher CPU)
- Consider increasing `MinimumCpuUsagePercent` or `MinimumPrivateMemoryMb` to reduce I/O

### Database Growing Too Fast
- Dual thresholds (CPU >= 1% OR Memory >= 100MB) save ~92% storage
- Automatic cleanup runs every 24 hours by default (configurable)
- Manual cleanup: `SELECT cleanup_old_snapshots(7);`
- Adjust retention via `DatabaseCleanup:RetentionDays` in appsettings.json
- Disable auto-cleanup: Set `DatabaseCleanup:EnableAutoCleanup` to false

## Testing

This project has comprehensive test coverage in **Slov89.PCStats.Service.Tests**.

### Service Component Tests

**ProcessMonitorServiceTests:**
- Tests process enumeration and filtering
- Validates CPU and memory metrics collection
- Tests VRAM detection logic
- Verifies system resource calculations

**HWiNFOServiceTests:**
- Tests HWiNFO shared memory access
- Validates temperature reading parsing
- Tests fallback behavior when HWiNFO not running
- Validates sensor data extraction

**OfflineStorageServiceTests:**
- Tests JSON file creation and storage
- Validates snapshot batch serialization
- Tests file retrieval and cleanup
- Verifies offline data retention policies

**OfflineDatabaseServiceTests:**
- Tests automatic offline mode switching
- Validates recovery process and data restoration
- Tests retry logic and error handling
- Verifies transaction integrity during recovery

### Running Tests

```powershell
# Run all service tests
dotnet test Slov89.PCStats.Service.Tests

# Run specific test class
dotnet test --filter FullyQualifiedName~ProcessMonitorServiceTests

# Run with detailed output
dotnet test Slov89.PCStats.Service.Tests --logger "console;verbosity=detailed"

# Run with code coverage
dotnet test Slov89.PCStats.Service.Tests --collect:"XPlat Code Coverage"
```

**Requirements:**
- Some tests may require elevated permissions for performance counter access
- HWiNFO tests require HWiNFO installed (will skip if not available)
- File system tests require write permissions to temp directories

## Related Documentation

- **[Main README](../README.md)** - Solution overview and architecture
- **[Data Layer Documentation](../Slov89.PCStats.Data/README.md)** - DatabaseService API reference
- **[Database Setup](../Database/README.md)** - Database initialization and schema
- **[Dashboard Documentation](../Slov89.PCStats.Dashboard/README.md)** - Visualizing collected data
- **[Models Documentation](../Slov89.PCStats.Models/README.md)** - Data models reference
- **[HWiNFO Diagnostics](../HWiNFODiagnostics/README.md)** - HWiNFO troubleshooting utility

## Target Framework

- .NET 10.0 (Windows)
