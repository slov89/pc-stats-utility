# Slov89.PCStats.Models

Shared data models (DTOs) used across all projects in the PC Stats Monitoring solution.

## Overview

This class library contains all the data transfer objects (DTOs) that represent the domain entities for system monitoring. These models are referenced by:
- **Slov89.PCStats.Data** - For database operations
- **Slov89.PCStats.Service** - For collecting and storing metrics
- **Slov89.PCStats.Dashboard** - For displaying metrics

## Models

### Snapshot.cs
Represents a system-level snapshot taken at a specific point in time.

**Properties:**
- `SnapshotId` (long) - Primary key
- `SnapshotTimestamp` (DateTime) - When the snapshot was taken
- `TotalCpuUsage` (decimal?) - Total CPU usage percentage (0-100)
- `TotalMemoryUsageMb` (long?) - Total memory in use (MB)
- `TotalAvailableMemoryMb` (long?) - Available memory (MB)

**Corresponds to:** `snapshots` table

### Process.cs
Represents a unique process definition.

**Properties:**
- `ProcessId` (int) - Primary key
- `ProcessName` (string) - Process executable name
- `ProcessPath` (string?) - Full path to executable
- `FirstSeen` (DateTime) - When first observed
- `LastSeen` (DateTime) - When last observed

**Corresponds to:** `processes` table

### ProcessSnapshot.cs
Represents process performance metrics at a specific snapshot.

**Properties:**
- `ProcessSnapshotId` (long) - Primary key
- `SnapshotId` (long) - Foreign key to Snapshot
- `ProcessId` (int) - Foreign key to Process
- `Pid` (int) - Process ID
- `CpuUsage` (decimal?) - CPU usage percentage
- `MemoryUsageMb` (long?) - Working set memory (MB)
- `PrivateMemoryMb` (long?) - Private memory (MB)
- `VirtualMemoryMb` (long?) - Virtual memory (MB)
- `VramUsageMb` (long?) - VRAM usage (MB)
- `ThreadCount` (int?) - Number of threads
- `HandleCount` (int?) - Number of handles

**Corresponds to:** `process_snapshots` table

### ProcessInfo.cs
DTO for transferring process information during collection (not directly mapped to database).

**Properties:**
- `ProcessName` (string) - Process name
- `ProcessPath` (string?) - Full path
- `Pid` (int) - Process ID
- `CpuUsage` (decimal) - CPU usage percentage
- `MemoryUsageMb` (long) - Memory in MB
- `PrivateMemoryMb` (long) - Private memory in MB
- `VirtualMemoryMb` (long) - Virtual memory in MB
- `VramUsageMb` (long?) - VRAM in MB (if available)
- `ThreadCount` (int) - Thread count
- `HandleCount` (int) - Handle count

### CpuTemperature.cs
Represents CPU temperature readings from HWiNFO.

**Properties:**
- `TempId` (long) - Primary key
- `SnapshotId` (long) - Foreign key to Snapshot
- `CpuTctlTdie` (decimal?) - CPU Tctl/Tdie temperature (°C)
- `CpuDieAverage` (decimal?) - Average die temperature (°C)
- `CpuCcd1Tdie` (decimal?) - CCD1 die temperature (°C)
- `CpuCcd2Tdie` (decimal?) - CCD2 die temperature (°C)
- `ThermalLimitPercent` (decimal?) - Thermal limit percentage
- `ThermalThrottling` (bool?) - Whether thermal throttling is active

**Corresponds to:** `cpu_temperatures` table

## Offline Storage Models

The following models support offline data storage when the database is unavailable:

### OfflineSnapshotData
Data structure for offline snapshot storage.

**Properties:**
- `TotalCpuUsage` (decimal?) - CPU usage percentage
- `TotalMemoryMb` (long?) - Total memory in MB
- `AvailableMemoryMb` (long?) - Available memory in MB
- `Timestamp` (DateTime) - Snapshot timestamp (UTC)
- `LocalSnapshotId` (long) - Local snapshot identifier

### OfflineProcessData
Data structure for offline process records.

**Properties:**
- `ProcessName` (string) - Process name
- `ProcessPath` (string?) - Process path
- `LocalProcessId` (int) - Local process identifier

### OfflineProcessSnapshotData
Data structure for offline process snapshot storage.

**Properties:**
- `LocalSnapshotId` (long) - Local snapshot ID reference
- `LocalProcessId` (int) - Local process ID reference
- `ProcessName` (string) - Process name
- `ProcessPath` (string?) - Process path
- `ProcessInfo` (ProcessInfo) - Full process metrics

### OfflineCpuTemperatureData
Data structure for offline CPU temperature storage.

**Properties:**
- `LocalSnapshotId` (long) - Local snapshot ID reference
- `Temperature` (CpuTemperature) - Temperature readings

### OfflineSnapshotBatch
Container for all offline operations in a single snapshot cycle.

**Properties:**
- `BatchId` (Guid) - Unique batch identifier
- `Timestamp` (DateTime) - Batch timestamp (UTC)
- `LocalSnapshotId` (long) - Local snapshot identifier
- `SnapshotData` (OfflineSnapshotData?) - System snapshot data
- `ProcessSnapshots` (List<OfflineProcessSnapshotData>) - Process metrics
- `CpuTemperature` (OfflineCpuTemperatureData?) - Temperature data
- `RetryCount` (int) - Number of restore attempts
- `ErrorMessage` (string?) - Last error message

**Usage:** Serialized to JSON files in `C:\\ProgramData\\Slov89.PCStats.Service\\OfflineData\\` when database is offline.

## Usage

Reference this project in your `.csproj`:

```xml
<ItemGroup>
  <ProjectReference Include="..\Slov89.PCStats.Models\Slov89.PCStats.Models.csproj" />
</ItemGroup>
```

Import the namespace:

```csharp
using Slov89.PCStats.Models;
```

## Dependencies

None - this is a plain class library with no external dependencies.

## Target Framework

- .NET 10.0
