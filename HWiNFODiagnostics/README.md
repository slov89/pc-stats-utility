# HWiNFO Diagnostics

Diagnostic utility for verifying HWiNFO shared memory access and discovering available CPU temperature sensors.

## Overview

This console application reads HWiNFO's shared memory to:
- Verify HWiNFO is running with shared memory enabled
- List all available CPU temperature sensors
- Display sensor names, units, and current values
- Help troubleshoot temperature monitoring integration

## Purpose

The main monitoring service ([Slov89.PCStats.Service](../Slov89.PCStats.Service/)) reads CPU temperatures from HWiNFO. This diagnostic tool helps:
- Confirm HWiNFO shared memory is accessible
- Identify which temperature sensors are available
- Troubleshoot why temperatures aren't being collected
- Verify sensor naming for code updates

## Usage

### Running the Tool

```powershell
cd HWiNFODiagnostics
dotnet run
```

Or run the compiled executable:

```powershell
cd HWiNFODiagnostics\bin\Debug\net10.0
.\HWiNFODiagnostics.exe
```

### Example Output (Success)

```
HWiNFO Sensor Diagnostics
=========================

✓ HWiNFO Shared Memory Found!
  Signature: 0x48575344
  Version: 2, Revision: 0
  Sensor Elements: 45 (offset: 48, size: 348)
  Reading Elements: 312 (offset: 18000, size: 300)

CPU Temperature Sensors:
================================================================================

#1: "CPU (Tctl/Tdie)"
     Unit: [°C]  Value: 45.5

#2: "CPU Die (average)"
     Unit: [°C]  Value: 43.2

#3: "CPU CCD1 (Tdie)"
     Unit: [°C]  Value: 44.0

#4: "CPU CCD2 (Tdie)"
     Unit: [°C]  Value: 42.5

✓ Found 4 CPU temperature sensor(s)
  (Also found 8 other temperature sensors)

Press any key to exit...
```

### Example Output (HWiNFO Not Running)

```
HWiNFO Sensor Diagnostics
=========================

✗ ERROR: HWiNFO shared memory not found!

Make sure:
  1. HWiNFO is running
  2. Shared Memory Support is enabled:
     - Open HWiNFO Settings
     - Go to 'Sensors' tab
     - Check 'Shared Memory Support'
     - Restart HWiNFO

Press any key to exit...
```

## Prerequisites

- **HWiNFO v8.14** or compatible version
- **Windows** operating system
- **.NET 10.0** Runtime

## HWiNFO Setup

For this diagnostic tool to work:

1. **Install HWiNFO**: Download from [hwinfo.com](https://www.hwinfo.com/)
2. **Enable Shared Memory**:
   - Launch HWiNFO
   - Click the Settings gear icon
   - Go to the "Sensors" tab
   - Enable "Shared Memory Support"
   - Click OK
   - Restart HWiNFO

3. **Keep HWiNFO Running**: The shared memory is only accessible while HWiNFO is running

## How It Works

The utility accesses HWiNFO's shared memory region (`Global\HWiNFO_SENS_SM2`) and:

1. **Reads the header** to get memory layout information
2. **Iterates through all sensor readings** in the shared memory
3. **Filters for temperature sensors** (unit contains "°C")
4. **Identifies CPU-related sensors** by looking for keywords:
   - "cpu", "core", "package", "ccd", "die"
5. **Displays sensor information** including:
   - Label/name
   - Unit of measurement
   - Current value

## Sensor Detection Logic

The tool identifies CPU temperature sensors using:

```csharp
bool isTemp = unit.Contains("C") && !unit.Contains("MHz") && !unit.Contains("Clock");
bool isCPU = label.ToLower().Contains("cpu") || 
             label.ToLower().Contains("core") || 
             label.ToLower().Contains("package") || 
             label.ToLower().Contains("ccd") || 
             label.ToLower().Contains("die");
```

## Troubleshooting

### No Sensors Found

If the tool runs but shows 0 CPU temperature sensors:

- **Check CPU compatibility**: Some CPUs don't expose temperature sensors
- **AMD Ryzen**: Should show Tctl/Tdie, CCD temperatures
- **Intel**: Should show Package, Core temperatures
- **Try running HWiNFO**: Let it run for a few seconds to initialize sensors

### Access Denied Error

- Run the tool as Administrator
- Check Windows permissions for memory-mapped files

### Wrong Sensor Names

If sensor names don't match what the service expects:

1. Note the exact sensor names from this tool's output
2. Update sensor name strings in [HWiNFOService.cs](../Slov89.PCStats.Service/Services/HWiNFOService.cs)
3. Rebuild the monitoring service

## Use Cases

### Initial Setup

Run this tool when first setting up temperature monitoring to verify HWiNFO configuration.

### Troubleshooting

If the monitoring service isn't collecting temperatures:

1. Stop the service
2. Run this diagnostic tool
3. Verify sensors are accessible
4. Compare sensor names with service code
5. Update configuration if needed

### Different Hardware

When deploying to different systems with different CPUs, use this tool to identify available sensors.

## Related Documentation

- **[Main README](../README.md)** - Solution overview
- **[Service Documentation](../Slov89.PCStats.Service/README.md)** - Temperature monitoring service
- **[HWiNFO Service Implementation](../Slov89.PCStats.Service/Services/)** - Temperature reading code

## Technical Details

### Memory Structure

The tool reads HWiNFO's shared memory structure:

- **Shared Memory Name**: `Global\HWiNFO_SENS_SM2`
- **Header Size**: 48 bytes
- **Contains**:
  - Signature and version info
  - Offsets and sizes for sensor and reading sections
  - Number of elements

### Data Structures

Uses P/Invoke structures matching HWiNFO's memory layout:

- `HWiNFO_SENSORS_SHARED_MEM2` - Header structure
- `HWiNFO_SENSORS_READING_ELEMENT` - Individual sensor reading

### Dependencies

- `System.Runtime.InteropServices` - For memory-mapped file access
- `System.IO.MemoryMappedFiles` - For shared memory reading

## Target Framework

- .NET 10.0

## Building

```powershell
dotnet build
```

Or build in Release mode:

```powershell
dotnet build -c Release
```

## Contributing

If you find sensors that should be detected but aren't:

1. Run the tool to get the exact sensor label
2. Update the detection logic in `Program.cs`
3. Test with your hardware
4. Submit changes with hardware information
