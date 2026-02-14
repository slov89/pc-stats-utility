using PCStats.Service;
using PCStats.Service.Services;
using PCStats.Data;

var builder = Host.CreateApplicationBuilder(args);

var pgConnectionString = Environment.GetEnvironmentVariable("slov89_pc_stats_utility_pg");
if (!string.IsNullOrEmpty(pgConnectionString))
{
    builder.Configuration["ConnectionStrings:PostgreSQL"] = pgConnectionString;
}

builder.Services.AddSingleton<IProcessMonitorService, ProcessMonitorService>();
builder.Services.AddSingleton<IHWiNFOService, HWiNFOService>();

builder.Services.AddSingleton<IOfflineStorageService, OfflineStorageService>();

builder.Services.AddSingleton<DatabaseService>();

builder.Services.AddSingleton<IDatabaseService>(provider =>
{
    var databaseService = provider.GetRequiredService<DatabaseService>();
    var offlineStorageService = provider.GetRequiredService<IOfflineStorageService>();
    var logger = provider.GetRequiredService<ILogger<OfflineDatabaseService>>();
    return new OfflineDatabaseService(databaseService, offlineStorageService, logger);
});

builder.Services.AddHostedService<Worker>();

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Slov89.PCStats.Service";
});

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddEventLog(settings =>
{
    settings.SourceName = "Slov89.PCStats.Service";
});

var host = builder.Build();
host.Run();
