# Database Schema

PostgreSQL database schema for the Slov89 PC Stats Monitoring solution.

## Overview

This database stores system performance metrics collected every 5 seconds by the monitoring service, including:
- System-level snapshots (CPU, memory)
- Process-level metrics (per-process CPU, memory, VRAM, threads, handles)
- CPU temperature readings (from HWiNFO)

The schema is designed for efficient time-series queries with proper indexing.

## Schema Version

- **Version**: 1.0
- **Database**: `pcstats` (recommended name)
- **PostgreSQL**: 12+

## Tables

### snapshots

Main table storing each monitoring interval.

**Columns:**
- `snapshot_id` (BIGSERIAL, PK) - Unique snapshot identifier
- `snapshot_timestamp` (TIMESTAMP) - When snapshot was taken (indexed)
- `total_cpu_usage` (DECIMAL 5,2) - System CPU usage percentage (0-100)
- `total_memory_usage_mb` (BIGINT) - Total memory in use (MB)
- `total_available_memory_mb` (BIGINT) - Available memory (MB)

**Indexes:**
- Primary key on `snapshot_id`
- Index on `snapshot_timestamp DESC` for time-range queries

**Frequency:** One record every 5 seconds (~17,280 records/day)

### processes

Master list of all unique processes ever observed.

**Columns:**
- `process_id` (SERIAL, PK) - Unique process identifier
- `process_name` (VARCHAR 255) - Process executable name (indexed)
- `process_path` (TEXT) - Full path to executable
- `first_seen` (TIMESTAMP) - When first observed
- `last_seen` (TIMESTAMP) - When last observed (indexed, updated on each appearance)

**Unique Constraint:** `(process_name, process_path)`

**Indexes:**
- Primary key on `process_id`
- Index on `process_name`
- Index on `last_seen DESC`

**Note:** ALL processes are tracked here, regardless of CPU usage threshold.

### process_snapshots

Performance metrics for each process at each snapshot. Only contains records for processes meeting the dual threshold (CPU >= 1% OR Memory >= 100MB by default).

**Columns:**
- `process_snapshot_id` (BIGSERIAL, PK) - Unique record identifier
- `snapshot_id` (BIGINT, FK) - References `snapshots.snapshot_id` (CASCADE DELETE)
- `process_id` (INTEGER, FK) - References `processes.process_id` (CASCADE DELETE)
- `pid` (INTEGER) - Operating system process ID
- `cpu_usage` (DECIMAL 5,2) - CPU usage percentage (indexed DESC)
- `memory_usage_mb` (BIGINT) - Working set memory in MB (indexed DESC)
- `private_memory_mb` (BIGINT) - Private memory in MB
- `virtual_memory_mb` (BIGINT) - Virtual memory in MB
- `vram_usage_mb` (BIGINT) - Video RAM usage in MB (if available)
- `thread_count` (INTEGER) - Number of threads
- `handle_count` (INTEGER) - Number of handles

**Indexes:**
- Primary key on `process_snapshot_id`
- Foreign key indexes on `snapshot_id` and `process_id`
- Performance indexes on `cpu_usage DESC` and `memory_usage_mb DESC`

**Storage:** With dual thresholds (CPU >= 1% OR Memory >= 100MB), ~10-30 processes per snapshot (~300,000 records/day)

### cpu_temperatures

CPU temperature readings from HWiNFO sensors. One record per snapshot (if HWiNFO is running).

**Columns:**
- `temp_id` (BIGSERIAL, PK) - Unique temperature record identifier
- `snapshot_id` (BIGINT, FK, UNIQUE) - References `snapshots.snapshot_id` (CASCADE DELETE)
- `cpu_tctl_tdie` (DECIMAL 5,2) - CPU Tctl/Tdie temperature (°C)
- `cpu_die_average` (DECIMAL 5,2) - Average die temperature (°C)
- `cpu_ccd1_tdie` (DECIMAL 5,2) - CCD1 die temperature (°C)
- `cpu_ccd2_tdie` (DECIMAL 5,2) - CCD2 die temperature (°C)
- `thermal_limit_percent` (DECIMAL 5,2) - Thermal limit percentage
- `thermal_throttling` (BOOLEAN) - Whether thermal throttling is active

**Indexes:**
- Primary key on `temp_id`
- Unique index on `snapshot_id`

**Note:** Records only exist if HWiNFO is running and sensors are available.

## Views

### v_latest_process_stats

Shows process performance metrics for the most recent snapshot.

**Columns:**
- `snapshot_timestamp` - When the snapshot was taken
- `process_name` - Process executable name
- `process_path` - Full path to executable
- `pid` - Operating system process ID
- `cpu_usage` - CPU usage percentage
- `memory_usage_mb` - Memory usage in MB
- `vram_usage_mb` - VRAM usage in MB
- `thread_count` - Number of threads

**Usage:**
```sql
SELECT * FROM v_latest_process_stats ORDER BY cpu_usage DESC LIMIT 10;
```

### v_latest_cpu_temps

Shows CPU temperature readings for the most recent snapshot.

**Columns:**
- `snapshot_timestamp` - When the snapshot was taken
- `cpu_tctl_tdie` - CPU Tctl/Tdie temperature
- `cpu_die_average` - Average die temperature
- `cpu_ccd1_tdie` - CCD1 temperature
- `cpu_ccd2_tdie` - CCD2 temperature
- `thermal_limit_percent` - Thermal limit percentage
- `thermal_throttling` - Whether throttling is active

**Usage:**
```sql
SELECT * FROM v_latest_cpu_temps;
```

## Functions

### cleanup_old_snapshots(days_to_keep)

Removes snapshot data older than the specified number of days.

**Parameters:**
- `days_to_keep` (INTEGER, default: 30) - Number of days of data to retain

**Returns:** INTEGER - Number of snapshots deleted

**Usage:**
```sql
-- Keep last 7 days
SELECT cleanup_old_snapshots(7);

-- Keep last 30 days (default)
SELECT cleanup_old_snapshots();
```

**Note:** 
- Due to CASCADE DELETE constraints, this also removes related `process_snapshots` and `cpu_temperatures` records
- The monitoring service automatically runs this function every 24 hours (configurable via `DatabaseCleanup` settings)

## Setup Instructions

### Option 1: Using PowerShell Script (Recommended)

```powershell
cd Database

# Interactive (password will be prompted securely)
.\Initialize-Database.ps1 -Server "localhost" -Port 5432 -Database "pcstats" -Username "postgres" -Password (Read-Host -AsSecureString -Prompt "Enter password")
```

The script will:
1. Test database connection
2. Create database if it doesn't exist
3. Run schema script to create tables, views, and functions

### Option 2: Manual Setup

```powershell
# Connect to PostgreSQL
psql -U postgres

# Create database
CREATE DATABASE pcstats;
\q

# Run schema script
psql -U postgres -d pcstats -f "01_InitialSchema.sql"
```

### Verify Setup

```sql
-- List all tables
\dt

-- Check table structures
\d snapshots
\d processes
\d process_snapshots
\d cpu_temperatures

-- View available views
\dv

-- View available functions
\df cleanup_old_snapshots
```

## Useful Queries

### Recent System Performance

```sql
-- Last 10 snapshots
SELECT 
    snapshot_timestamp,
    total_cpu_usage,
    total_memory_usage_mb,
    total_available_memory_mb
FROM snapshots 
ORDER BY snapshot_timestamp DESC 
LIMIT 10;
```

### Top CPU Consumers (Last Hour)

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

### Top Memory Consumers (Last Hour)

```sql
SELECT 
    p.process_name,
    AVG(ps.memory_usage_mb) as avg_memory_mb,
    MAX(ps.memory_usage_mb) as max_memory_mb,
    MAX(ps.vram_usage_mb) as max_vram_mb
FROM process_snapshots ps
JOIN processes p ON ps.process_id = p.process_id
JOIN snapshots s ON ps.snapshot_id = s.snapshot_id
WHERE s.snapshot_timestamp > NOW() - INTERVAL '1 hour'
GROUP BY p.process_name
ORDER BY avg_memory_mb DESC
LIMIT 10;
```

### CPU Temperature Trends (Last Hour)

```sql
SELECT 
    s.snapshot_timestamp,
    ct.cpu_tctl_tdie,
    ct.cpu_die_average,
    ct.cpu_ccd1_tdie,
    ct.cpu_ccd2_tdie,
    ct.thermal_throttling
FROM cpu_temperatures ct
JOIN snapshots s ON ct.snapshot_id = s.snapshot_id
WHERE s.snapshot_timestamp > NOW() - INTERVAL '1 hour'
ORDER BY s.snapshot_timestamp DESC;
```

### Process Lifetime Activity

```sql
-- When each process was first and last seen
SELECT 
    process_name,
    first_seen,
    last_seen,
    last_seen - first_seen as tracked_duration,
    COUNT(ps.process_snapshot_id) as active_snapshots
FROM processes p
LEFT JOIN process_snapshots ps ON p.process_id = ps.process_id
GROUP BY p.process_id, p.process_name, p.first_seen, p.last_seen
ORDER BY last_seen DESC
LIMIT 20;
```

### Database Statistics

```sql
-- Row counts per table
SELECT 
    'snapshots' as table_name, 
    COUNT(*) as row_count,
    pg_size_pretty(pg_total_relation_size('snapshots')) as total_size
FROM snapshots
UNION ALL
SELECT 'processes', COUNT(*), pg_size_pretty(pg_total_relation_size('processes'))
FROM processes
UNION ALL
SELECT 'process_snapshots', COUNT(*), pg_size_pretty(pg_total_relation_size('process_snapshots'))
FROM process_snapshots
UNION ALL
SELECT 'cpu_temperatures', COUNT(*), pg_size_pretty(pg_total_relation_size('cpu_temperatures'))
FROM cpu_temperatures;
```

### Data Retention Check

```sql
-- Age of oldest and newest data
SELECT 
    MIN(snapshot_timestamp) as oldest_snapshot,
    MAX(snapshot_timestamp) as newest_snapshot,
    MAX(snapshot_timestamp) - MIN(snapshot_timestamp) as retention_period,
    COUNT(*) as total_snapshots
FROM snapshots;
```

## Performance Considerations

### Storage Estimates

With default settings (5 second intervals, CPU >= 1% OR Memory >= 100MB thresholds):
- **Snapshots**: ~850 KB/day
- **Process Snapshots**: ~31 MB/day (10-30 processes per snapshot)
- **CPU Temperatures**: ~700 KB/day
- **Total**: ~33 MB/day

Without thresholds (all processes):
- **Process Snapshots**: ~377 MB/day (200+ processes per snapshot)
- **Total**: ~408 MB/day

**Storage savings with dual thresholds: ~92%**

### Index Optimization

All frequently-queried columns are indexed:
- **Time-based queries**: `snapshots.snapshot_timestamp` (DESC)
- **Process lookups**: `processes.process_name`, `processes.last_seen`
- **Performance queries**: `process_snapshots.cpu_usage` (DESC), `process_snapshots.memory_usage_mb` (DESC)
- **Foreign keys**: All FK columns indexed

### Query Performance Tips

1. Always filter by time range when querying snapshots
2. Use indexes: filter on `snapshot_timestamp`, `process_name`, `cpu_usage`, or `memory_usage_mb`
3. Limit results for large time ranges
4. Consider materialized views for complex aggregations

## Maintenance

### Automatic Cleanup

The monitoring service automatically runs the cleanup function every 24 hours (configurable via `DatabaseCleanup` settings in appsettings.json).

### Manual Cleanup

Run cleanup function periodically (e.g., weekly via cron or scheduled task):

```sql
-- Keep last 30 days
SELECT cleanup_old_snapshots(30);
```

### Vacuum and Analyze

After large deletions, optimize the database:

```sql
VACUUM ANALYZE snapshots;
VACUUM ANALYZE process_snapshots;
VACUUM ANALYZE cpu_temperatures;
```

### Monitor Database Size

```sql
SELECT pg_size_pretty(pg_database_size('pcstats'));
```

### Backup

Regular backups recommended:

```bash
# Backup
pg_dump -U postgres -d pcstats -F c -f pcstats_backup_$(date +%Y%m%d).dump

# Restore
pg_restore -U postgres -d pcstats -c pcstats_backup_20260212.dump
```

## Related Documentation

- **[Main README](../README.md)** - Solution overview and quick start guide
- **[Data Layer Documentation](../Slov89.PCStats.Data/README.md)** - Database operations API\n- **[Service Documentation](../Slov89.PCStats.Service/README.md)** - Service that writes to database
- **[Dashboard Documentation](../Slov89.PCStats.Dashboard/README.md)** - Querying and visualizing data
- **[Models Documentation](../Slov89.PCStats.Models/README.md)** - Data model definitions

## Troubleshooting

### Connection Issues

```bash
# Test connection
psql -U postgres -d pcstats -c "SELECT version();"

# Check PostgreSQL is running
# Windows:
Get-Service postgresql*

# Check database exists
psql -U postgres -c "\l" | grep pcstats
```

### Missing Data

```sql
-- Check if service is writing data
SELECT MAX(snapshot_timestamp) FROM snapshots;

-- Should be within last 5-10 seconds if service is running
```

### Performance Issues

```sql
-- Check for missing indexes
SELECT schemaname, tablename, indexname 
FROM pg_indexes 
WHERE schemaname = 'public' 
ORDER BY tablename;

-- Check slow queries (if enabled)
SELECT * FROM pg_stat_statements 
ORDER BY total_time DESC 
LIMIT 10;
```

## Schema Evolution

To modify the schema:
1. Back up the database first
2. Create migration script with ALTER statements
3. Test on a copy before applying to production
4. Document changes in version history

## Related Documentation

- **[Main README](../README.md)** - Solution overview
- **[Service Documentation](../Slov89.PCStats.Service/README.md)** - Data collection service
- **[Dashboard Documentation](../Slov89.PCStats.Dashboard/README.md)** - Web visualization
- **[Data Layer Documentation](../Slov89.PCStats.Data/README.md)** - Database operations

## Notes

- All timestamps are in server local time
- Decimal precision for percentages: 5 digits, 2 decimal places (e.g., 100.00)
- Foreign key constraints use CASCADE DELETE for automatic cleanup
- The schema supports multiple CPU temperature sensors for AMD processors
- VRAM monitoring may not work on all systems (field will be NULL)
