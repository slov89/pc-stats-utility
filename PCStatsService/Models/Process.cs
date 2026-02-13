namespace PCStatsService.Models;

public class Process
{
    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string? ProcessPath { get; set; }
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
}
