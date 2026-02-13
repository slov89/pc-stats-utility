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
- **Smart Filtering**: Only saves detailed metrics for processes with CPU usage >= threshold (default 5%)
- **Process Tracking**: All processes tracked in database, regardless of threshold
- **HWiNFO Integration**: Optional CPU temperature monitoring
- **Windows Service**: Runs in background, starts with Windows
- **Configurable**: All settings in `appsettings.json`

## Architecture

### Services

#### ProcessMonitorService
Collects process information and performance metrics.

**Methods:**
- `GetRunningProcessesAsync()` - Enumerate all running processes
- Internal: CPU usage calculation, VRAM detection, performance counter reading

#### HWiNFOService
Reads CPU temperature data from HWiNFO shared memory.

**Methods:**
- `GetCpuTemperaturesAsync()` - Read current CPU temperatures
- Falls back to registry if shared memory unavailable

#### Worker
Main background service that orchestrates data collection.

**Flow:**
1. Collect system metrics (CPU, memory)
2. Collect all running processes
3. Read CPU temperatures (if HWiNFO running)
4. Create snapshot in database
5. Track all processes in `processes` table
6. Filter processes by CPU threshold
7. Save detailed metrics for filtered processes
8. Wait for configured interval, repeat

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
      "Default": "Information",
      "Slov89.PCStats.Service": "Information"
    }
  },
  "MonitoringSettings": {
    "IntervalSeconds": 5,
    "EnableVRAMMonitoring": true,
    "EnableCPUTemperatureMonitoring": true,
    "MinimumCpuUsagePercent": 5.0
  }
}
```

**Settings:**
- `IntervalSeconds` - Collection interval (default: 5)
- `EnableVRAMMonitoring` - Enable VRAM tracking (may not work on all GPUs)
- `EnableCPUTemperatureMonitoring` - Enable HWiNFO temperature monitoring
- `MinimumCpuUsagePercent` - CPU threshold for detailed metrics (0 = all processes)

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
sc.exe create Slov89PCStatsService binPath="C:\Services\Slov89.PCStats.Service\Slov89.PCStats.Service.exe" start=auto
sc.exe description Slov89PCStatsService "Monitors PC performance stats and logs to PostgreSQL database"
sc.exe start Slov89PCStatsService
```

## HWiNFO Setup (Optional)

For CPU temperature monitoring:

1. Install [HWiNFO v8.14](https://www.hwinfo.com/)
2. Open HWiNFO Settings (gear icon)
3. Enable "Shared Memory Support"
4. Click OK and restart HWiNFO
5. Keep HWiNFO running in background

**Note:** Service runs without HWiNFO, it just won't collect temperature data.

## Service Management

### Start/Stop

```powershell
# Start
Start-Service Slov89PCStatsService

# Stop
Stop-Service Slov89PCStatsService

# Status
Get-Service Slov89PCStatsService
```

### View Logs

1. Open Event Viewer (`eventvwr.msc`)
2. Navigate to: Windows Logs → Application
3. Filter by source: "Slov89PCStatsService"

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
Stop-Service Slov89PCStatsService
sc.exe delete Slov89PCStatsService
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
- Consider increasing `MinimumCpuUsagePercent` to reduce I/O

### Database Growing Too Fast
- Use CPU threshold (default 5% saves ~92% storage)
- Run periodic cleanup: `SELECT cleanup_old_snapshots(7);`
- Adjust retention based on needs

## Target Framework

- .NET 10.0 (Windows)
