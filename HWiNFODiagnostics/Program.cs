using System.Runtime.InteropServices;
using System.IO.MemoryMappedFiles;

Console.WriteLine("HWiNFO Sensor Diagnostics");
Console.WriteLine("=========================\n");

const string HWINFO_SHARED_MEM_NAME = "Global\\HWiNFO_SENS_SM2";

try
{
    using var mmf = MemoryMappedFile.OpenExisting(HWINFO_SHARED_MEM_NAME, MemoryMappedFileRights.Read);
    using var accessor = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

    byte[] headerBytes = new byte[48];
    accessor.ReadArray(0, headerBytes, 0, headerBytes.Length);
    
    uint dwSignature = BitConverter.ToUInt32(headerBytes, 0);
    uint dwVersion = BitConverter.ToUInt32(headerBytes, 4);
    uint dwRevision = BitConverter.ToUInt32(headerBytes, 8);
    uint dwOffsetOfSensorSection = BitConverter.ToUInt32(headerBytes, 20);
    uint dwSizeOfSensorElement = BitConverter.ToUInt32(headerBytes, 24);
    uint dwNumSensorElements = BitConverter.ToUInt32(headerBytes, 28);
    uint dwOffsetOfReadingSection = BitConverter.ToUInt32(headerBytes, 32);
    uint dwSizeOfReadingElement = BitConverter.ToUInt32(headerBytes, 36);
    uint dwNumReadingElements = BitConverter.ToUInt32(headerBytes, 40);

    Console.WriteLine($"✓ HWiNFO Shared Memory Found!");
    Console.WriteLine($"  Signature: 0x{dwSignature:X}");
    Console.WriteLine($"  Version: {dwVersion}, Revision: {dwRevision}");
    Console.WriteLine($"  Sensor Elements: {dwNumSensorElements} (offset: {dwOffsetOfSensorSection}, size: {dwSizeOfSensorElement})");
    Console.WriteLine($"  Reading Elements: {dwNumReadingElements} (offset: {dwOffsetOfReadingSection}, size: {dwSizeOfReadingElement})\n");

    Console.WriteLine("CPU Temperature Sensors:");
    Console.WriteLine(new string('=', 80));

    var cpuTempCount = 0;
    var allTempCount = 0;
    
    for (uint i = 0; i < dwNumReadingElements; i++)
    {
        long offset = dwOffsetOfReadingSection + (i * dwSizeOfReadingElement);
        
        byte[] sensorBytes = new byte[dwSizeOfReadingElement];
        accessor.ReadArray(offset, sensorBytes, 0, (int)dwSizeOfReadingElement);
        
        byte[] labelBytes = new byte[128];
        Array.Copy(sensorBytes, 12, labelBytes, 0, 128);
        
        byte[] unitBytes = new byte[16];
        Array.Copy(sensorBytes, 268, unitBytes, 0, 16);
        
        double value = BitConverter.ToDouble(sensorBytes, 284);
        
        var label = System.Text.Encoding.ASCII.GetString(labelBytes).TrimEnd('\0');
        var unit = System.Text.Encoding.ASCII.GetString(unitBytes).TrimEnd('\0');
        
        bool isTemp = unit.Contains("C") && !unit.Contains("MHz") && !unit.Contains("Clock");
        bool isCPU = label.ToLower().Contains("cpu") || label.ToLower().Contains("core") || label.ToLower().Contains("package") || label.ToLower().Contains("ccd") || label.ToLower().Contains("die");
        
        if (isTemp && isCPU)
        {
            cpuTempCount++;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\n#{cpuTempCount}: \"{label}\"");
            Console.ResetColor();
            Console.WriteLine($"     Unit: [{unit}]  Value: {value:F1}");
        }
        else if (isTemp)
        {
            allTempCount++;
        }
    }

    if (cpuTempCount == 0)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\nNo CPU temperature sensors found!");
        Console.WriteLine($"(Found {allTempCount} non-CPU temperature sensors like GPU, drives, etc.)");
        Console.WriteLine("\nYour CPU might not have temperature sensors exposed in HWiNFO shared memory.");
        Console.ResetColor();
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n✓ Found {cpuTempCount} CPU temperature sensor(s)");
        if (allTempCount > 0)
        {
            Console.WriteLine($"  (Also found {allTempCount} other temperature sensors)");
        }
        Console.ResetColor();
    }
}
catch (FileNotFoundException)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("✗ ERROR: HWiNFO shared memory not found!");
    Console.ResetColor();
    Console.WriteLine("\nMake sure:");
    Console.WriteLine("  1. HWiNFO is running");
    Console.WriteLine("  2. Shared Memory Support is enabled:");
    Console.WriteLine("     - Open HWiNFO Settings");
    Console.WriteLine("     - Go to 'Sensors' tab");
    Console.WriteLine("     - Check 'Shared Memory Support'");
    Console.WriteLine("     - Restart HWiNFO");
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"✗ ERROR: {ex.Message}");
    Console.ResetColor();
}

Console.WriteLine("\nPress any key to exit...");
Console.ReadKey();

[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct HWiNFO_SENSORS_SHARED_MEM2
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
struct HWiNFO_SENSORS_READING_ELEMENT
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
