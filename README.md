# Slov89 PC Stats Monitoring

A comprehensive .NET 10 monitoring solution that collects Windows system performance metrics every 5 seconds and provides a real-time web dashboard for visualization.

## Overview

This solution consists of four main components:

1. **[Slov89.PCStats.Models](Slov89.PCStats.Models/)** - Shared data models (DTOs)
2. **[Slov89.PCStats.Data](Slov89.PCStats.Data/)** - Data access layer for PostgreSQL
3. **[Slov89.PCStats.Service](Slov89.PCStats.Service/)** - Windows service that collects metrics
4. **[Slov89.PCStats.Dashboard](Slov89.PCStats.Dashboard/)** - Blazor Server web dashboard

Data is stored in PostgreSQL with an efficient normalized schema, indexed for fast querying.

## Key Features

- **Automated Monitoring**: Collects system and process metrics every 5 seconds
- **Smart Filtering**: Only saves detailed metrics for processes with CPU usage >= threshold (default 5%)
- **CPU Temperature Tracking**: Integrates with HWiNFO v8.14 for AMD CPU temperature sensors
- **Real-time Dashboard**: Interactive web UI with charts for CPU, memory, temperatures, and top processes
- **Time Range Filtering**: View metrics for 5, 10, 30, or 60 minutes
- **Storage Optimization**: ~92% storage reduction with CPU threshold filtering
- **Offline Storage**: Automatically saves data to JSON files when database is unavailable, with automatic recovery
- **Zero Data Loss**: Continuous monitoring even during database outages
- **Windows Service**: Runs in background, starts with Windows

## Quick Start

### 1. Setup Database

```powershell
cd Database
.\Initialize-Database.ps1 -Server "localhost" -Port 5432 -Database "pcstats" -Username "postgres" -Password (Read-Host -AsSecureString)
```

See [Database/README.md](Database/README.md) for details.

### 2. Configure Connection String

```powershell
.\Set-ConnectionString.ps1 -ConnectionString "Host=localhost;Port=5432;Database=pcstats;Username=postgres;Password=YOUR_PASSWORD"
```

This sets the `slov89_pc_stats_utility_pg` environment variable.

### 3. Install Windows Service

```powershell
cd Slov89.PCStats.Service
.\Install-Service.ps1
```

See [Slov89.PCStats.Service/README.md](Slov89.PCStats.Service/README.md) for details.

### 4. Run Dashboard

```powershell
cd Slov89.PCStats.Dashboard
dotnet run
```

Navigate to `https://localhost:5001`

See [Slov89.PCStats.Dashboard/README.md](Slov89.PCStats.Dashboard/README.md) for details.

## Prerequisites

- **Windows 10/11** or Windows Server 2019+
- **.NET 10 SDK** (for building) or .NET 10 Runtime (for running)
- **PostgreSQL 12+** database server
- **HWiNFO v8.14** (optional, for CPU temperature monitoring)

## Architecture Highlights

### Offline Storage & Recovery

The service includes robust offline storage capabilities:

- **Automatic Detection**: Detects database connection failures automatically
- **Local Storage**: Saves snapshots to JSON files in `C:\ProgramData\Slov89.PCStats.Service\OfflineData\`
- **Automatic Recovery**: When database reconnects, all offline data is restored in chronological order
- **Data Integrity**: Maintains all relationships between snapshots, processes, and temperatures
- **Configurable Retention**: Old offline files auto-cleaned after 7 days (configurable)
- **Zero Data Loss**: Monitoring continues uninterrupted during database outages

## Solution Structure

```
slov89-pc-stats-utility/
├── Database/                          # Database schema and setup scripts
│   ├── 01_InitialSchema.sql          
│   ├── Initialize-Database.ps1       
│   └── README.md                      📖 Database documentation
│
├── Slov89.PCStats.Models/            # Shared data models
│   └── README.md                      📖 Models documentation
│
├── Slov89.PCStats.Data/              # Data access layer
│   ├── DatabaseService.cs             # Write operations (used by Service)
│   ├── MetricsService.cs              # Read operations (used by Dashboard)
│   └── README.md                      📖 Data layer documentation
│
├── Slov89.PCStats.Service/           # Windows monitoring service
│   ├── Install-Service.ps1           
│   ├── Uninstall-Service.ps1         
│   └── README.md                      📖 Service documentation
│
├── Slov89.PCStats.Dashboard/         # Web dashboard
│   └── README.md                      📖 Dashboard documentation
│
├── Set-ConnectionString.ps1          # Connection string helper
└── README.md                          # This file
```

## Documentation

### Project-Specific Guides

- **[Models Documentation](Slov89.PCStats.Models/README.md)** - Data models and DTOs
- **[Data Layer Documentation](Slov89.PCStats.Data/README.md)** - Database operations and queries
- **[Service Documentation](Slov89.PCStats.Service/README.md)** - Installation, configuration, troubleshooting
- **[Dashboard Documentation](Slov89.PCStats.Dashboard/README.md)** - Running, deploying, customizing
- **[Database Documentation](Database/README.md)** - Schema, views, functions, queries

### What Each Component Does

#### Slov89.PCStats.Models
Plain C# classes representing domain entities (Snapshot, Process, CpuTemperature, etc.). Referenced by all other projects.

#### Slov89.PCStats.Data
Provides two main services:
- **DatabaseService** - Write operations (create snapshots, save process data)
- **MetricsService** - Read operations (query time-series data for dashboard)

#### Slov89.PCStats.Service
Background Windows service that:
1. Collects system metrics (CPU, memory) every 5 seconds
2. Enumerates running processes and their metrics
3. Reads CPU temperatures from HWiNFO (if running)
4. Stores data in PostgreSQL via DatabaseService
5. Automatically saves to offline JSON storage if database is unavailable
6. Restores offline data when database reconnects

#### Slov89.PCStats.Dashboard
Blazor Server web application that:
1. Queries metrics data via MetricsService
2. Displays interactive charts (ApexCharts)
3. Allows time range filtering (5/10/30/60 minutes)
4. Shows real-time system performance

## Technologies Used

- **.NET 10** - Framework
- **C#** - Programming language
- **PostgreSQL 12+** - Database
- **Blazor Server** - Web UI framework
- **ApexCharts** - Charting library
- **Bootstrap 5** - UI components
- **Npgsql 10.0.1** - PostgreSQL driver
- **HWiNFO v8.14** - Hardware monitoring (optional)

## Database Schema

The PostgreSQL database contains:

- **snapshots** - System metrics (CPU, memory) every 5 seconds
- **processes** - Master list of all processes ever seen
- **process_snapshots** - Per-process metrics (only for processes with CPU >= threshold)
- **cpu_temperatures** - CPU temperature readings from HWiNFO

See [Database/README.md](Database/README.md) for full schema, views, and useful queries.

## Performance & Storage

### Data Volume (with 5% CPU threshold)
- **12 snapshots/minute** (5-second intervals)
- **~10-30 active processes per snapshot** (filtered from ~200 total)
- **~350,000 records/day**
- **~33 MB/day** (vs ~408 MB without filtering)
- **Storage savings: ~92%**

### Cleanup

Use the provided function to remove old data:
```sql
SELECT cleanup_old_snapshots(7);  -- Keep last 7 days
```

## Development

### Build Solution

```powershell
dotnet build
```

### Run Tests

```powershell
# Run service in console mode
cd Slov89.PCStats.Service
dotnet run

# Run dashboard in development mode
cd Slov89.PCStats.Dashboard
dotnet run
```

### Environment Variables

Both Service and Dashboard read the connection string from:
- **Variable Name**: `slov89_pc_stats_utility_pg`
- **Set via**: `Set-ConnectionString.ps1` script
- **Scope**: Machine (persists across reboots, available to services)

## Common Tasks

### Check Service Status
```powershell
Get-Service Slov89.PCStats.Service
```

### View Service Logs
1. Event Viewer → Windows Logs → Application
2. Filter by source: "Slov89.PCStats.Service"

### Check Offline Storage
```powershell
# View offline data files
Get-ChildItem "C:\ProgramData\Slov89.PCStats.Service\OfflineData" -Filter "*.json"

# Count pending offline snapshots
(Get-ChildItem "C:\ProgramData\Slov89.PCStats.Service\OfflineData" -Filter "snapshot_*.json").Count
```

### Access Dashboard
Navigate to `https://localhost:5001` (or configured URL)

### Query Database
```sql
-- View latest snapshots
SELECT * FROM snapshots ORDER BY snapshot_timestamp DESC LIMIT 10;

-- View latest process stats
SELECT * FROM v_latest_process_stats LIMIT 20;

-- View CPU temperatures
SELECT * FROM v_latest_cpu_temps;
```

More queries in [Database/README.md](Database/README.md).

## Troubleshooting

### Service Issues
See [Slov89.PCStats.Service/README.md](Slov89.PCStats.Service/README.md#troubleshooting)

### Dashboard Issues
See [Slov89.PCStats.Dashboard/README.md](Slov89.PCStats.Dashboard/README.md#troubleshooting)

### Database Issues
See [Database/README.md](Database/README.md)

### General Checks
- Verify environment variable: `[Environment]::GetEnvironmentVariable('slov89_pc_stats_utility_pg', 'Machine')`
- Check PostgreSQL running: `Get-Service postgresql*`
- Test database connection: `psql -U postgres -d pcstats`

## Version History

### v2.1 - Offline Storage & Recovery
- Added automatic offline storage when database is unavailable
- Automatic bulk restoration of offline data when database reconnects
- Zero data loss during database outages
- Configurable offline file retention (default: 7 days)
- JSON-based offline storage in `C:\ProgramData\Slov89.PCStats.Service\OfflineData`
- Retry logic with automatic cleanup
- Enhanced logging for offline/recovery operations

### v2.0 - Multi-project refactor and dashboard
- Reorganized into multi-project solution (Models, Data, Service, Dashboard)
- Created Blazor Server web dashboard with real-time visualization
- ApexCharts integration for interactive graphs
- Time range filtering (5/10/30/60 minutes)
- Moved all database operations to shared Data project
- Environment variable configuration (`slov89_pc_stats_utility_pg`)
- Separated interfaces and implementations

### v1.0 - Initial release
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

For detailed information:
- 📖 [Service Documentation](Slov89.PCStats.Service/README.md)
- 📖 [Dashboard Documentation](Slov89.PCStats.Dashboard/README.md)
- 📖 [Data Layer Documentation](Slov89.PCStats.Data/README.md)
- 📖 [Database Documentation](Database/README.md)

---

**Built with .NET 10 | Blazor Server | PostgreSQL | ApexCharts | HWiNFO v8.14**
