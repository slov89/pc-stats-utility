-- PC Stats Monitoring Database Schema
-- Version: 1.0
-- Description: Tracks running processes, their resource usage, and CPU temperatures

-- Drop tables if they exist (for clean re-creation)
DROP TABLE IF EXISTS process_snapshots CASCADE;
DROP TABLE IF EXISTS cpu_temperatures CASCADE;
DROP TABLE IF EXISTS snapshots CASCADE;
DROP TABLE IF EXISTS processes CASCADE;

-- Main snapshots table - records each monitoring interval
CREATE TABLE snapshots (
    snapshot_id BIGSERIAL PRIMARY KEY,
    snapshot_timestamp TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    total_cpu_usage DECIMAL(5,2), -- Overall system CPU usage percentage
    total_memory_usage_mb BIGINT, -- Total system memory usage in MB
    total_available_memory_mb BIGINT -- Available system memory in MB
);

-- Create index on timestamp for efficient time-based queries
CREATE INDEX idx_snapshots_timestamp ON snapshots(snapshot_timestamp DESC);

-- Processes table - stores unique process information
CREATE TABLE processes (
    process_id SERIAL PRIMARY KEY,
    process_name VARCHAR(255) NOT NULL,
    process_path TEXT,
    first_seen TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_seen TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT uq_process_name_path UNIQUE(process_name, process_path)
);

CREATE INDEX idx_processes_name ON processes(process_name);
CREATE INDEX idx_processes_last_seen ON processes(last_seen DESC);

-- Process snapshots - links processes to snapshots with performance metrics
CREATE TABLE process_snapshots (
    process_snapshot_id BIGSERIAL PRIMARY KEY,
    snapshot_id BIGINT NOT NULL REFERENCES snapshots(snapshot_id) ON DELETE CASCADE,
    process_id INTEGER NOT NULL REFERENCES processes(process_id) ON DELETE CASCADE,
    pid INTEGER NOT NULL, -- Operating system process ID (can change between runs)
    cpu_usage DECIMAL(5,2), -- CPU usage percentage
    memory_usage_mb BIGINT, -- Working set memory in MB
    private_memory_mb BIGINT, -- Private memory in MB
    virtual_memory_mb BIGINT, -- Virtual memory in MB
    vram_usage_mb BIGINT, -- Video RAM usage in MB (if available)
    thread_count INTEGER, -- Number of threads
    handle_count INTEGER -- Number of handles
);

CREATE INDEX idx_process_snapshots_snapshot ON process_snapshots(snapshot_id);
CREATE INDEX idx_process_snapshots_process ON process_snapshots(process_id);
CREATE INDEX idx_process_snapshots_cpu_usage ON process_snapshots(cpu_usage DESC);
CREATE INDEX idx_process_snapshots_memory ON process_snapshots(memory_usage_mb DESC);

-- CPU temperatures table - stores temperature readings from HWiNFO
-- One record per snapshot with individual columns for each sensor
CREATE TABLE cpu_temperatures (
    temp_id BIGSERIAL PRIMARY KEY,
    snapshot_id BIGINT NOT NULL UNIQUE REFERENCES snapshots(snapshot_id) ON DELETE CASCADE,
    cpu_tctl_tdie DECIMAL(5,2), -- CPU (Tctl/Tdie) temperature
    cpu_die_average DECIMAL(5,2), -- CPU Die (average) temperature
    cpu_ccd1_tdie DECIMAL(5,2), -- CPU CCD1 (Tdie) temperature
    cpu_ccd2_tdie DECIMAL(5,2), -- CPU CCD2 (Tdie) temperature
    thermal_limit_percent DECIMAL(5,2), -- Thermal Limit percentage
    thermal_throttling BOOLEAN -- Thermal Throttling (HTC) yes/no
);

CREATE INDEX idx_cpu_temps_snapshot ON cpu_temperatures(snapshot_id);

-- Create a view for easy querying of complete snapshot data
CREATE VIEW v_latest_process_stats AS
SELECT 
    s.snapshot_timestamp,
    p.process_name,
    p.process_path,
    ps.pid,
    ps.cpu_usage,
    ps.memory_usage_mb,
    ps.vram_usage_mb,
    ps.thread_count
FROM snapshots s
INNER JOIN process_snapshots ps ON s.snapshot_id = ps.snapshot_id
INNER JOIN processes p ON ps.process_id = p.process_id
WHERE s.snapshot_id = (SELECT MAX(snapshot_id) FROM snapshots);

-- Create a view for latest CPU temperatures
CREATE VIEW v_latest_cpu_temps AS
SELECT 
    s.snapshot_timestamp,
    ct.cpu_tctl_tdie,
    ct.cpu_die_average,
    ct.cpu_ccd1_tdie,
    ct.cpu_ccd2_tdie,
    ct.thermal_limit_percent,
    ct.thermal_throttling
FROM snapshots s
INNER JOIN cpu_temperatures ct ON s.snapshot_id = ct.snapshot_id
WHERE s.snapshot_id = (SELECT MAX(snapshot_id) FROM snapshots);

-- Function to clean up old data (optional, for maintenance)
CREATE OR REPLACE FUNCTION cleanup_old_snapshots(days_to_keep INTEGER DEFAULT 30)
RETURNS INTEGER AS $$
DECLARE
    deleted_count INTEGER;
BEGIN
    DELETE FROM snapshots 
    WHERE snapshot_timestamp < NOW() - (days_to_keep || ' days')::INTERVAL;
    
    GET DIAGNOSTICS deleted_count = ROW_COUNT;
    RETURN deleted_count;
END;
$$ LANGUAGE plpgsql;

-- Comments for documentation
COMMENT ON TABLE snapshots IS 'Main table storing each monitoring snapshot with timestamp and system-level metrics';
COMMENT ON TABLE processes IS 'Unique process definitions tracked across snapshots';
COMMENT ON TABLE process_snapshots IS 'Performance metrics for each process at each snapshot';
COMMENT ON TABLE cpu_temperatures IS 'CPU temperature readings from HWiNFO sensors';
