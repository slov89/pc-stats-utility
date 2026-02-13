using System.Runtime.InteropServices;
using Microsoft.Win32;
using PCStatsService.Models;

namespace PCStatsService.Services;

public interface IHWiNFOService
{
    Task<CpuTemperature?> GetCpuTemperaturesAsync();
    bool IsHWiNFORunning();
}

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
            using var mmf = System.IO.MemoryMappedFiles.MemoryMappedFile.OpenExisting(
                HWINFO_SHARED_MEM_NAME, 
                System.IO.MemoryMappedFiles.MemoryMappedFileRights.Read);

            using var accessor = mmf.CreateViewAccessor(0, 0, System.IO.MemoryMappedFiles.MemoryMappedFileAccess.Read);

            // Read header
            HWiNFO_SENSORS_SHARED_MEM2 header;
            accessor.Read(0, out header);

            // Validate signature
            if (header.dwSignature != 0x57494E48) // "HWIN" in ASCII
            {
                _logger.LogWarning("Invalid HWiNFO shared memory signature");
                return null;
            }

            // Read temperature readings
            for (uint i = 0; i < header.dwNumReadingElements; i++)
            {
                long offset = header.dwOffsetOfReadingSection + (i * header.dwSizeOfReadingElement);
                
                HWiNFO_SENSORS_READING_ELEMENT reading;
                accessor.Read(offset, out reading);

                // Check if this is a temperature sensor (unit contains "°C" or "C")
                var unit = System.Text.Encoding.ASCII.GetString(reading.szUnit).TrimEnd('\0');
                var label = System.Text.Encoding.ASCII.GetString(reading.szLabelOrig).TrimEnd('\0');

                // Map specific sensors to the appropriate property
                if (unit.Contains("°C") || unit.Contains("C"))
                {
                    var temp = (decimal)reading.Value;
                    
                    if (label.Equals("CPU (Tctl/Tdie)", StringComparison.OrdinalIgnoreCase))
                    {
                        cpuTemp.CpuTctlTdie = temp;
                        foundAny = true;
                        _logger.LogTrace("Captured CPU (Tctl/Tdie): {Temp}°C", temp);
                    }
                    else if (label.Equals("CPU Die (average)", StringComparison.OrdinalIgnoreCase))
                    {
                        cpuTemp.CpuDieAverage = temp;
                        foundAny = true;
                        _logger.LogTrace("Captured CPU Die (average): {Temp}°C", temp);
                    }
                    else if (label.Equals("CPU CCD1 (Tdie)", StringComparison.OrdinalIgnoreCase))
                    {
                        cpuTemp.CpuCcd1Tdie = temp;
                        foundAny = true;
                        _logger.LogTrace("Captured CPU CCD1 (Tdie): {Temp}°C", temp);
                    }
                    else if (label.Equals("CPU CCD2 (Tdie)", StringComparison.OrdinalIgnoreCase))
                    {
                        cpuTemp.CpuCcd2Tdie = temp;
                        foundAny = true;
                        _logger.LogTrace("Captured CPU CCD2 (Tdie): {Temp}°C", temp);
                    }
                }
            }

            if (foundAny)
            {
                _logger.LogDebug("Read CPU temperatures from HWiNFO shared memory");
                return cpuTemp;
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
