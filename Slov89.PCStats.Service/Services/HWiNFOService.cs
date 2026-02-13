using System.Runtime.InteropServices;
using Microsoft.Win32;
using Slov89.PCStats.Models;

namespace Slov89.PCStats.Service.Services;

public class HWiNFOService : IHWiNFOService
{
    private readonly ILogger<HWiNFOService> _logger;
    private const string HWINFO_SHARED_MEM_NAME = "Global\\HWiNFO_SENS_SM2";
    
    // HWiNFO Shared Memory structures
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct HWiNFO_SENSORS_SHARED_MEM2
    {
        public uint dwSignature;
        public uint dwVersion;
        public uint dwRevision;
        public long poll_time;
        public uint dwOffsetOfSensorSection;
        public uint dwSizeOfSensorElement;
        public uint dwNumSensorElements;
        public uint dwOffsetOfReadingSection;
        public uint dwSizeOfReadingElement;
        public uint dwNumReadingElements;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct HWiNFO_SENSORS_READING_ELEMENT
    {
        public uint dwReadingId;
        public uint dwSensorIndex;
        public uint dwSensorId;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
        public byte[] szLabelOrig;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
        public byte[] szLabelUser;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] szUnit;
        public double Value;
        public double ValueMin;
        public double ValueMax;
        public double ValueAvg;
    }

    public HWiNFOService(ILogger<HWiNFOService> logger)
    {
        _logger = logger;
    }

    public bool IsHWiNFORunning()
    {
        try
        {
            var processes = System.Diagnostics.Process.GetProcessesByName("HWiNFO64");
            if (processes.Length == 0)
            {
                processes = System.Diagnostics.Process.GetProcessesByName("HWiNFO32");
            }
            return processes.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    public Task<CpuTemperature?> GetCpuTemperaturesAsync()
    {
        try
        {
            if (!IsHWiNFORunning())
            {
                _logger.LogWarning("HWiNFO is not running. CPU temperature data will not be available.");
                return Task.FromResult<CpuTemperature?>(null);
            }

            // Try to read from HWiNFO shared memory
            var temperatures = ReadFromHWiNFOSharedMemory();
            if (temperatures != null)
            {
                return Task.FromResult<CpuTemperature?>(temperatures);
            }

            // Fallback: Try reading from Registry (older HWiNFO versions)
            temperatures = ReadFromHWiNFORegistry();
            if (temperatures != null)
            {
                return Task.FromResult<CpuTemperature?>(temperatures);
            }

            _logger.LogWarning("Could not read temperature data from HWiNFO");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading CPU temperatures from HWiNFO");
        }

        return Task.FromResult<CpuTemperature?>(null);
    }

    private CpuTemperature? ReadFromHWiNFOSharedMemory()
    {
        var cpuTemp = new CpuTemperature();
        var foundAny = false;

        try
        {
            _logger.LogDebug("Opening HWiNFO shared memory...");
            using var mmf = System.IO.MemoryMappedFiles.MemoryMappedFile.OpenExisting(
                HWINFO_SHARED_MEM_NAME, 
                System.IO.MemoryMappedFiles.MemoryMappedFileRights.Read);

            using var accessor = mmf.CreateViewAccessor(0, 0, System.IO.MemoryMappedFiles.MemoryMappedFileAccess.Read);

            // Read header
            HWiNFO_SENSORS_SHARED_MEM2 header;
            accessor.Read(0, out header);

            // Validate signature (HWiNFO uses 0x53695748)
            if (header.dwSignature != 0x53695748)
            {
                _logger.LogWarning("Invalid HWiNFO shared memory signature: 0x{Signature:X}", header.dwSignature);
                return null;
            }

            _logger.LogDebug("HWiNFO shared memory opened. Reading {Count} sensors...", header.dwNumReadingElements);

            // Read temperature readings
            int tempSensorsFound = 0;
            for (uint i = 0; i < header.dwNumReadingElements; i++)
            {
                long offset = header.dwOffsetOfReadingSection + (i * header.dwSizeOfReadingElement);
                
                // Read bytes manually instead of using struct marshaling (more reliable)
                byte[] sensorBytes = new byte[header.dwSizeOfReadingElement];
                accessor.ReadArray(offset, sensorBytes, 0, (int)header.dwSizeOfReadingElement);
                
                // Extract label (offset 12, size 128)
                byte[] labelBytes = new byte[128];
                Array.Copy(sensorBytes, 12, labelBytes, 0, 128);
                
                // Extract unit (offset 268, size 16)
                byte[] unitBytes = new byte[16];
                Array.Copy(sensorBytes, 268, unitBytes, 0, 16);
                
                // Extract value (offset 284, 8 bytes double)
                double value = BitConverter.ToDouble(sensorBytes, 284);
                
                var label = System.Text.Encoding.ASCII.GetString(labelBytes).TrimEnd('\0');
                var unit = System.Text.Encoding.ASCII.GetString(unitBytes).TrimEnd('\0');

                // Map specific sensors to the appropriate property
                // Note: In HWiNFO 8.14+, the degree symbol may be encoded as '?' so check for both "°C" and "?C"
                // Exclude units like "Clock", "MHz", etc. that might contain 'C'
                bool isTemperatureSensor = (unit.Contains("°C") || unit.Contains("?C")) && 
                                          !unit.Contains("Clock") && 
                                          !unit.Contains("MHz");
                
                if (isTemperatureSensor)
                {
                    tempSensorsFound++;
                    var temp = (decimal)value;
                    
                    if (label.Equals("CPU (Tctl/Tdie)", StringComparison.OrdinalIgnoreCase))
                    {
                        cpuTemp.CpuTctlTdie = temp;
                        foundAny = true;
                        _logger.LogInformation("Captured CPU (Tctl/Tdie): {Temp}°C", temp);
                    }
                    else if (label.Equals("CPU Die (average)", StringComparison.OrdinalIgnoreCase))
                    {
                        cpuTemp.CpuDieAverage = temp;
                        foundAny = true;
                        _logger.LogInformation("Captured CPU Die (average): {Temp}°C", temp);
                    }
                    else if (label.Equals("CPU CCD1 (Tdie)", StringComparison.OrdinalIgnoreCase))
                    {
                        cpuTemp.CpuCcd1Tdie = temp;
                        foundAny = true;
                        _logger.LogInformation("Captured CPU CCD1 (Tdie): {Temp}°C", temp);
                    }
                    else if (label.Equals("CPU CCD2 (Tdie)", StringComparison.OrdinalIgnoreCase))
                    {
                        cpuTemp.CpuCcd2Tdie = temp;
                        foundAny = true;
                        _logger.LogInformation("Captured CPU CCD2 (Tdie): {Temp}°C", temp);
                    }
                    else
                    {
                        // Log other temperature sensors we're ignoring
                        _logger.LogTrace("Ignoring temperature sensor: {Label} = {Temp}°C", label, temp);
                    }
                }
                else if (unit.Contains("%") && label.Equals("Thermal Limit", StringComparison.OrdinalIgnoreCase))
                {
                    cpuTemp.ThermalLimitPercent = (decimal)value;
                    foundAny = true;
                    _logger.LogTrace("Captured Thermal Limit: {Value}%", value);
                }
                else if (label.Equals("Thermal Throttling (HTC)", StringComparison.OrdinalIgnoreCase))
                {
                    cpuTemp.ThermalThrottling = value > 0;
                    foundAny = true;
                    _logger.LogTrace("Captured Thermal Throttling: {Value}", value > 0 ? "Yes" : "No");
                }
            }

            _logger.LogDebug("Scanned {TotalSensors} sensors, found {TempSensors} temperature sensors, matched {MatchedSensors} CPU temps", 
                header.dwNumReadingElements, tempSensorsFound, foundAny ? "some" : "none");

            if (foundAny)
            {
                _logger.LogInformation("Successfully read CPU temperatures from HWiNFO shared memory");
                return cpuTemp;
            }
            else
            {
                _logger.LogWarning("Found {TempCount} temperature sensors but none matched expected CPU sensor names", tempSensorsFound);
            }
        }
        catch (FileNotFoundException)
        {
            _logger.LogDebug("HWiNFO shared memory not found. Make sure HWiNFO is running with shared memory enabled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading from HWiNFO shared memory");
        }

        return null;
    }

    private CpuTemperature? ReadFromHWiNFORegistry()
    {
        var cpuTemp = new CpuTemperature();
        var foundAny = false;

        try
        {
            // HWiNFO can write sensor data to registry
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\HWiNFO64\VSB");
            if (key == null)
            {
                _logger.LogDebug("HWiNFO registry key not found");
                return null;
            }

            var valueNames = key.GetValueNames();
            foreach (var valueName in valueNames)
            {
                var value = key.GetValue(valueName);
                if (value != null && decimal.TryParse(value.ToString(), out var temp))
                {
                    if (valueName.Equals("CPU (Tctl/Tdie)", StringComparison.OrdinalIgnoreCase))
                    {
                        cpuTemp.CpuTctlTdie = temp;
                        foundAny = true;
                        _logger.LogTrace("Captured CPU (Tctl/Tdie) from registry: {Temp}°C", temp);
                    }
                    else if (valueName.Equals("CPU Die (average)", StringComparison.OrdinalIgnoreCase))
                    {
                        cpuTemp.CpuDieAverage = temp;
                        foundAny = true;
                        _logger.LogTrace("Captured CPU Die (average) from registry: {Temp}°C", temp);
                    }
                    else if (valueName.Equals("CPU CCD1 (Tdie)", StringComparison.OrdinalIgnoreCase))
                    {
                        cpuTemp.CpuCcd1Tdie = temp;
                        foundAny = true;
                        _logger.LogTrace("Captured CPU CCD1 (Tdie) from registry: {Temp}°C", temp);
                    }
                    else if (valueName.Equals("CPU CCD2 (Tdie)", StringComparison.OrdinalIgnoreCase))
                    {
                        cpuTemp.CpuCcd2Tdie = temp;
                        foundAny = true;
                        _logger.LogTrace("Captured CPU CCD2 (Tdie) from registry: {Temp}°C", temp);
                    }
                    else if (valueName.Equals("Thermal Limit", StringComparison.OrdinalIgnoreCase))
                    {
                        cpuTemp.ThermalLimitPercent = temp;
                        foundAny = true;
                        _logger.LogTrace("Captured Thermal Limit from registry: {Value}%", temp);
                    }
                    else if (valueName.Equals("Thermal Throttling (HTC)", StringComparison.OrdinalIgnoreCase))
                    {
                        cpuTemp.ThermalThrottling = temp > 0;
                        foundAny = true;
                        _logger.LogTrace("Captured Thermal Throttling from registry: {Value}", temp > 0 ? "Yes" : "No");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading from HWiNFO registry");
        }

        return foundAny ? cpuTemp : null;
    }
}
