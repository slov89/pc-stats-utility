using Slov89.PCStats.Models;

namespace Slov89.PCStats.Service.Services;

public interface IHWiNFOService
{
    Task<CpuTemperature?> GetCpuTemperaturesAsync();
    bool IsHWiNFORunning();
}
