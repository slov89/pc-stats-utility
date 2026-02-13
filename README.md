# PC Stats Monitoring Service

A .NET 10 Windows service that monitors system performance and logs data to PostgreSQL database every 5 seconds.

## Overview

This service runs in the background on Windows and collects comprehensive system metrics:
- All running processes with smart filtering
- CPU, memory, and VRAM usage per process
- CPU temperature sensors (via HWiNFO)
- System-level metrics

Data is stored in PostgreSQL with an efficient normalized schema, indexed for fast querying.

## Features

- **Process Monitoring**: Tracks all running processes on the system
- **Smart Filtering**: All processes tracked in master list, but only processes with CPU usage >= threshold (default 5%) get detailed metrics saved
- **Performance Metrics**: Records CPU usage, memory usage (working set, private, virtual), VRAM usage, thread count, and handle count for active processes
- **CPU Temperature Monitoring**: Integrates with HWiNFO v8.14 to capture specific AMD CPU sensors (Tctl/Tdie, Die average, CCD1, CCD2)
- **PostgreSQL Database**: Stores all metrics in a normalized relational database for efficient querying
- **Windows Service**: Runs in the background as a Windows service
- **5-Second Intervals**: Collects metrics every 5 seconds
- **Storage Optimization**: ~92% storage reduction with CPU threshold filtering

## Technologies Used

- **.NET 10** - Latest .NET framework
- **C#** - Programming language
- **PostgreSQL 12+** - Database
- **Npgsql 10.0.1** - PostgreSQL driver for .NET
- **System.Diagnostics.PerformanceCounter** - Process and performance counter APIs
- **System.Management** - WMI for VRAM monitoring
- **Microsoft.Extensions.Hosting.WindowsServices** - Background service framework
- **HWiNFO v8.14** - Third-party hardware monitoring tool (optional)

## Project Structure

```
slov89-pc-stats-utility/
├── Database/
│   ├── 01_InitialSchema.sql          # PostgreSQL database schema
│   ├── Initialize-Database.ps1       # Database setup script
│   └── README.md                      # Database documentation
│
├── PCStatsService/
│   ├── Models/
│   │   ├── Snapshot.cs                # Main snapshot model
│   │   ├── Process.cs                 # Process model
│   │   ├── ProcessSnapshot.cs         # Process performance metrics model
│   │   ├── CpuTemperature.cs         # CPU temperature model
│   │   └── ProcessInfo.cs            # Process information DTO
│   │
│   ├── Services/
│   │   ├── ProcessMonitorService.cs   # Monitors running processes
│   │   ├── HWiNFOService.cs          # Reads CPU temps from HWiNFO
│   │   └── DatabaseService.cs        # PostgreSQL operations
│   │
│   ├── Worker.cs                      # Main background worker
│   ├── Program.cs                     # Service configuration
│   ├── appsettings.json              # Configuration
│   ├── appsettings.Development.json  # Development configuration
│   └── PCStatsService.csproj         # Project file (.NET 10)
│
├── Install-Service.ps1                # Installation script
├── Uninstall-Service.ps1             # Uninstallation script  
├── README.md                          # This file
└── .gitignore                         # Git ignore file
```

## Prerequisites

- Windows 10/11 or Windows Server 2019+
- .NET 10 SDK (for building)
- .NET 10 Runtime (for running)
- PostgreSQL 12+ database server
- HWiNFO v8.14 (for CPU temperature monitoring)
  - Must be configured to enable shared memory support

## Installation

### Step 1: Database Setup

**Option A: Using the PowerShell script (Recommended)**

This script will automatically create the database if it doesn't exist.

```powershell
cd Database

# Interactive (password will be prompted securely)
.\Initialize-Database.ps1 -Server "localhost" -Port 5432 -Database "pc_stats_monitoring" -Username "postgres" -Password (Read-Host -AsSecureString -Prompt "Enter PostgreSQL password")

# Or with inline SecureString (less secure, but useful for scripts)
$SecurePassword = ConvertTo-SecureString "your_password" -AsPlainText -Force
.\Initialize-Database.ps1 -Server "localhost" -Port 5432 -Database "pc_stats_monitoring" -Username "postgres" -Password $SecurePassword
```

**Option B: Manual setup**

```powershell
# Connect to PostgreSQL
psql -U postgres

# Create database
CREATE DATABASE pc_stats_monitoring;
\q

# Run schema script
psql -U postgres -d pc_stats_monitoring -f "Database\01_InitialSchema.sql"
```

### Step 2: Configure the Service

Edit `PCStatsService\appsettings.json` and update the connection string:

```json
{
  "ConnectionStrings": {
    "PostgreSQL": "Host=localhost;Port=5432;Database=pc_stats_monitoring;Username=postgres;Password=YOUR_PASSWORD"
  },
  "MonitoringSettings": {
    "IntervalSeconds": 5,
    "EnableVRAMMonitoring": true,
    "EnableCPUTemperatureMonitoring": true,
    "MinimumCpuUsagePercent": 5.0  // Only save detailed metrics for processes with CPU >= this %
                                    // Set to 0 to save detailed metrics for all processes
  }
}
```

### Step 3: Configure HWiNFO (Optional)

If you want CPU temperature monitoring:

1. Install HWiNFO v8.14
2. Open HWiNFO Settings (click the Settings/gear button)
3. Go to "Shared Memory Support" section
4. Check "Enable Shared Memory Support"
5. Click OK and restart HWiNFO
6. Keep HWiNFO running in the background

Note: The service will run without HWiNFO, it just won't collect temperature data.

### Step 4: Install as Windows Service

**Option A: Using the installation script (Recommended)**

Open PowerShell as Administrator:

```powershell
cd "C:\Users\ezupe\Documents\Development\slov89-pc-stats-utility"
.\Install-Service.ps1
```

The script will:
- Build the project
- Publish to `C:\Services\PCStatsService`
- Create and configure the Windows service
- Prompt you to start the service

**Option B: Manual installation**

**Option B: Manual installation**

```powershell
# Build the service
cd PCStatsService
dotnet build -c Release

# Publish the service
dotnet publish -c Release -o C:\Services\PCStatsService

# Install as Windows Service (as Administrator)
sc.exe create PCStatsMonitoringService binPath="C:\Services\PCStatsService\PCStatsService.exe" start=auto
sc.exe description PCStatsMonitoringService "Monitors PC performance stats and logs to PostgreSQL database"
sc.exe start PCStatsMonitoringService
```

### Step 5: Verify Installation

**Check service status:**
```powershell
Get-Service PCStatsMonitoringService
```

**Check Event Logs:**
1. Open Event Viewer (`eventvwr.msc`)
2. Go to Windows Logs → Application
3. Filter by source: "PCStatsMonitoringService"
4. You should see: "PC Stats Service started. Monitoring every 5 seconds..."

**Check Database:**
```sql
-- Connect to database
psql -U postgres -d pc_stats_monitoring

-- View latest snapshots
SELECT * FROM snapshots ORDER BY snapshot_id DESC LIMIT 10;

-- View latest process stats
SELECT * FROM v_latest_process_stats LIMIT 20;

-- View CPU temperatures (if HWiNFO is running)
SELECT * FROM v_latest_cpu_temps;
```

## What's Being Monitored

Every 5 seconds, the service records:

### System Level
- Total CPU usage percentage
- Total memory usage in MB
- Available memory in MB

### Per Process
All processes are tracked in the `processes` table. For processes with CPU usage >= threshold (default 5%):
- Process name and file path
- CPU usage percentage
- Memory usage (working set, private, virtual) in MB
- VRAM usage in MB (if available via GPU drivers)
- Thread count
- Handle count

### CPU Temperatures (if HWiNFO is running)
- CPU (Tctl/Tdie) - Main CPU temperature
- CPU Die (average) - Average die temperature
- CPU CCD1 (Tdie) - Core Complex Die 1 temperature
- CPU CCD2 (Tdie) - Core Complex Die 2 temperature

## Database Schema

The database uses a normalized relational design with 4 main tables:

### Tables
1. **snapshots** - Main table for each monitoring interval (every 5 seconds)
   - `snapshot_id` (primary key)
   - `snapshot_timestamp`
   - `total_cpu_usage`, `total_memory_usage_mb`, `total_available_memory_mb`

2. **processes** - Unique process definitions (all processes ever seen)
   - `process_id` (primary key)
   - `process_name`, `process_path`
   - `first_seen`, `last_seen`
   - **Note**: All processes tracked here regardless of CPU usage

3. **process_snapshots** - Performance metrics for each process at each snapshot
   - `process_snapshot_id` (primary key)
   - Foreign keys: `snapshot_id`, `process_id`
   - Metrics: `cpu_usage`, memory usage, VRAM, threads, handles
   - **Note**: Only contains records for processes with CPU usage >= configured threshold (default 5%)

4. **cpu_temperatures** - CPU temperature readings from HWiNFO
   - `temp_id` (primary key)
   - `snapshot_id` (foreign key, one record per snapshot)
   - `cpu_tctl_tdie`, `cpu_die_average`, `cpu_ccd1_tdie`, `cpu_ccd2_tdie`

### Views
- **v_latest_process_stats** - Most recent process statistics
- **v_latest_cpu_temps** - Most recent CPU temperatures

### Functions
- **cleanup_old_snapshots(days_to_keep)** - Removes old data for maintenance

See [Database/README.md](Database/README.md) for detailed schema information.

## Performance & Storage

### Data Volume (with 5% CPU threshold filtering)
- **12 snapshots/minute** (5-second intervals)
- **720 snapshots/hour**
- **~10-30 active processes per snapshot** (filtered from ~200 total)
- **~14,400 process snapshot records/hour** (instead of 144,000 without filtering)
- **~350,000 records/day**

### Storage Estimates
Daily storage with 5% CPU threshold:
- Snapshots table: ~850 KB
- Process snapshots table (filtered): ~31 MB
- CPU temperatures: ~700 KB
- **Total: ~33 MB/day**

Without filtering (all processes): ~408 MB/day

**Storage savings: ~92% with default threshold**

### Optimization Tips
- Use `cleanup_old_snapshots()` function regularly
- Adjust `MinimumCpuUsagePercent` based on your needs (0 = log all processes)
- Consider archiving old data to separate tables
- Use database partitioning for very large datasets

Example cleanup (keep last 7 days):
```sql
SELECT cleanup_old_snapshots(7);
```

## Useful Queries

### Top 10 CPU consuming processes in the last hour
```sql
SELECT 
    p.process_name,
    AVG(ps.cpu_usage) as avg_cpu,
    MAX(ps.cpu_usage) as max_cpu,
    COUNT(*) as sample_count
FROM process_snapshots ps
JOIN processes p ON ps.process_id = p.process_id
JOIN snapshots s ON ps.snapshot_id = s.snapshot_id
WHERE s.snapshot_timestamp > NOW() - INTERVAL '1 hour'
GROUP BY p.process_name
ORDER BY avg_cpu DESC
LIMIT 10;
```

### Top 10 memory consuming processes in the last hour
```sql
SELECT 
    p.process_name,
    AVG(ps.memory_usage_mb) as avg_memory_mb,
    MAX(ps.memory_usage_mb) as max_memory_mb
FROM process_snapshots ps
JOIN processes p ON ps.process_id = p.process_id
JOIN snapshots s ON ps.snapshot_id = s.snapshot_id
WHERE s.snapshot_timestamp > NOW() - INTERVAL '1 hour'
GROUP BY p.process_name
ORDER BY avg_memory_mb DESC
LIMIT 10;
```

### CPU temperature trends over the last hour
```sql
SELECT 
    s.snapshot_timestamp,
    ct.cpu_tctl_tdie,
    ct.cpu_die_average,
    ct.cpu_ccd1_tdie,
    ct.cpu_ccd2_tdie
FROM cpu_temperatures ct
JOIN snapshots s ON ct.snapshot_id = s.snapshot_id
WHERE s.snapshot_timestamp > NOW() - INTERVAL '1 hour'
ORDER BY s.snapshot_timestamp DESC;
```

### Process activity timeline (when a process was active)
```sql
SELECT 
    p.process_name,
    p.first_seen,
    p.last_seen,
    p.last_seen - p.first_seen as total_tracked_duration,
    COUNT(ps.process_snapshot_id) as active_snapshots
FROM processes p
LEFT JOIN process_snapshots ps ON p.process_id = ps.process_id
GROUP BY p.process_id, p.process_name, p.first_seen, p.last_seen
ORDER BY p.last_seen DESC
LIMIT 20;
```

## Service Management

### Start the service
```powershell
Start-Service PCStatsMonitoringService
# or
sc.exe start PCStatsMonitoringService
```

### Stop the service
```powershell
Stop-Service PCStatsMonitoringService
# or
sc.exe stop PCStatsMonitoringService
```

### View service status
```powershell
Get-Service PCStatsMonitoringService
# or
sc.exe query PCStatsMonitoringService
```

### View service logs
Check Windows Event Viewer:
- Open Event Viewer (`eventvwr.msc`)
- Navigate to: Windows Logs → Application
- Filter by source: "PCStatsMonitoringService"

The service logs:
- **Information**: Service start/stop, snapshot creation
- **Warning**: HWiNFO not running, connection issues
- **Error**: Database errors, critical failures

### Uninstall the service
```powershell
# Using the uninstall script
.\Uninstall-Service.ps1

# Or manually
Stop-Service PCStatsMonitoringService
sc.exe delete PCStatsMonitoringService
```

## Development & Testing

### Run in console mode (for testing)

```powershell
cd PCStatsService
dotnet run
```

Press Ctrl+C to stop.

### Run with debug logging

Edit `appsettings.Development.json` and set log level to "Debug", then:

```powershell
dotnet run --environment Development
```

### Build the project

```powershell
dotnet build -c Release
```

## Troubleshooting

### Service won't start
- **Check Event Viewer** for detailed error messages (source: "PCStatsMonitoringService")
- **Verify PostgreSQL connection string** is correct in `appsettings.json`
- **Ensure PostgreSQL service is running**: `Get-Service postgresql*`
- **Verify database schema** has been created: `psql -U postgres -d pc_stats_monitoring -c "\dt"`
- **Check file permissions** on `C:\Services\PCStatsService\` folder

### No CPU temperature data
- **Ensure HWiNFO v8.14 is installed and running**
- **Verify "Shared Memory Support"** is enabled in HWiNFO settings
- **This feature is optional** - the service will run without it
- Check Event Viewer logs for HWiNFO-related warnings
- Verify you have an AMD processor with supported sensors

### High CPU/Memory usage by the service
- **Normal CPU usage**: 1-3% on average
- **Check monitoring interval**: Default is 5 seconds (configurable in `appsettings.json`)
- **Consider increasing CPU threshold**: Higher `MinimumCpuUsagePercent` = less data stored
- **Review process count**: 200+ processes can increase processing time
- Check Event Viewer for performance warnings

### Database growing too fast
- **Use CPU threshold filtering**: Default 5% saves ~92% storage
- **Run cleanup regularly**: `SELECT cleanup_old_snapshots(7);` to keep last 7 days
- **Adjust retention policy**: Keep only data you need
- **Consider archiving**: Move old data to archive tables
- **Monitor database size**: `SELECT pg_size_pretty(pg_database_size('pc_stats_monitoring'));`

### VRAM data not showing
- **VRAM monitoring requires specific GPU drivers** and may not work on all systems
- **Integrated graphics** (Intel, AMD APU) may not report VRAM usage
- **Check logs** for VRAM-related errors in Event Viewer
- **This is optional** - service continues without VRAM data
- You can disable VRAM monitoring in `appsettings.json`: `"EnableVRAMMonitoring": false`

### Connection errors to PostgreSQL
- **Verify PostgreSQL is running**: `Get-Service postgresql*`
- **Test connection manually**: `psql -U postgres -d pc_stats_monitoring`
- **Check firewall settings** if PostgreSQL is on a remote server
- **Verify username/password** in connection string
- **Check PostgreSQL logs** for authentication errors

### Process data seems incomplete
- **Check CPU threshold setting**: Processes below `MinimumCpuUsagePercent` won't have detailed metrics
- **All processes are tracked** in `processes` table regardless of threshold
- **Only active processes** (CPU >= threshold) get entries in `process_snapshots`
- Set `MinimumCpuUsagePercent` to 0 to log all processes (increases storage ~10x)

## Configuration Reference

### appsettings.json

```json
{
  "ConnectionStrings": {
    "PostgreSQL": "Host=localhost;Port=5432;Database=pc_stats_monitoring;Username=postgres;Password=your_password"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "PCStatsService": "Information"
    }
  },
  "MonitoringSettings": {
    "IntervalSeconds": 5,                     // Monitoring interval in seconds
    "EnableVRAMMonitoring": true,             // Enable VRAM monitoring (may not work on all systems)
    "EnableCPUTemperatureMonitoring": true,   // Enable HWiNFO temperature monitoring
    "MinimumCpuUsagePercent": 5.0             // Only save detailed metrics for processes with CPU >= this %
                                               // Set to 0 to save all processes (increases storage)
  }
}
```

## Architecture

### Service Components

```
PCStatsService (Worker Service)
├── Models/
│   ├── Snapshot.cs           # Snapshot data model
│   ├── Process.cs            # Process data model
│   ├── ProcessSnapshot.cs    # Process snapshot data model
│   ├── CpuTemperature.cs    # CPU temperature data model
│   └── ProcessInfo.cs       # Process info DTO
├── Services/
│   ├── ProcessMonitorService.cs    # Monitors running processes
│   │   - GetRunningProcessesAsync()
│   │   - CalculateProcessCpuUsage()
│   │   - GetProcessVRAM()
│   │
│   ├── HWiNFOService.cs            # Reads CPU temps from HWiNFO
│   │   - GetCpuTemperaturesAsync()
│   │   - ReadFromHWiNFOSharedMemory()
│   │   - ReadFromHWiNFORegistry() [fallback]
│   │
│   └── DatabaseService.cs          # PostgreSQL operations
│       - CreateSnapshotAsync()
│       - GetOrCreateProcessAsync()
│       - CreateProcessSnapshotAsync()
│       - CreateCpuTemperatureAsync()
│
├── Worker.cs                        # Main background worker
│   - ExecuteAsync() - Main service loop
│   - CollectAndLogStatsAsync() - Data collection orchestration
│
└── Program.cs                       # Service configuration & DI setup
```

### Data Flow

1. **Worker** executes every 5 seconds
2. **ProcessMonitorService** collects process information and metrics
3. **HWiNFOService** reads CPU temperatures (if HWiNFO is running)
4. **Worker** creates snapshot and tracks all processes in master table
5. **Worker** filters processes by CPU threshold
6. **DatabaseService** persists:
   - Snapshot with system metrics
   - All processes to `processes` table
   - Filtered process metrics to `process_snapshots` table
   - CPU temperatures to `cpu_temperatures` table
7. Cycle repeats

### Dependencies

- **Npgsql** - PostgreSQL database driver
- **System.Diagnostics.PerformanceCounter** - CPU/memory performance counters
- **System.Management** - WMI queries for VRAM
- **Microsoft.Extensions.Hosting.WindowsServices** - Windows service hosting

## Future Enhancement Ideas

1. **Web Dashboard** - Real-time monitoring interface
2. **Alerting System** - Email/SMS alerts for high CPU/memory usage
3. **GPU Monitoring** - Comprehensive GPU metrics beyond just VRAM
4. **Network I/O** - Track network usage per process
5. **Disk I/O** - Track disk read/write per process
6. **Process Filtering** - Configurable include/exclude process lists
7. **Data Export** - Export to CSV, JSON, or Excel
8. **REST API** - API endpoints for data access
9. **Automated Reports** - Daily/weekly summary reports
10. **Multi-Machine Support** - Agent architecture for monitoring multiple PCs
11. **Historical Analysis** - Trend analysis and anomaly detection
12. **Custom Metrics** - Plugin system for custom metrics

## Version History

- **v1.0** - Initial release
  - Process monitoring with smart filtering
  - CPU temperature monitoring (HWiNFO integration)  
  - PostgreSQL storage with normalized schema
  - Windows service implementation
  - 5-second monitoring intervals
  - Storage optimization with CPU threshold

## License

[Add your license here]

## Contributing

[Add contribution guidelines here]

## Support

For issues, questions, or contributions:
- Check [troubleshooting](#troubleshooting) section first
- Review Windows Event Viewer logs
- Check PostgreSQL logs
- Verify database schema with [Database/README.md](Database/README.md)

---

**Built with .NET 10 | PostgreSQL | HWiNFO v8.14**
