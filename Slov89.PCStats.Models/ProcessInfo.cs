namespace Slov89.PCStats.Models;

public class ProcessInfo
{
    public int Pid { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string? ProcessPath { get; set; }
    public decimal CpuUsage { get; set; }
    public long MemoryUsageMb { get; set; }
    public long PrivateMemoryMb { get; set; }
    public long VirtualMemoryMb { get; set; }
    public long VramUsageMb { get; set; }
    public int ThreadCount { get; set; }
    public int HandleCount { get; set; }
}
