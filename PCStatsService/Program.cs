using PCStatsService;
using PCStatsService.Services;

var builder = Host.CreateApplicationBuilder(args);

// Configure services
builder.Services.AddSingleton<IProcessMonitorService, ProcessMonitorService>();
builder.Services.AddSingleton<IHWiNFOService, HWiNFOService>();
builder.Services.AddSingleton<IDatabaseService, DatabaseService>();
builder.Services.AddHostedService<Worker>();

// Configure Windows Service
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "PCStatsMonitoringService";
});

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddEventLog(settings =>
{
    settings.SourceName = "PCStatsMonitoringService";
});

var host = builder.Build();
host.Run();
