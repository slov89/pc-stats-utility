using Slov89.PCStats.Service;
using Slov89.PCStats.Service.Services;
using Slov89.PCStats.Data;

var builder = Host.CreateApplicationBuilder(args);

// Load connection string from environment variable
var pgConnectionString = Environment.GetEnvironmentVariable("slov89_pc_stats_utility_pg");
if (!string.IsNullOrEmpty(pgConnectionString))
{
    builder.Configuration["ConnectionStrings:PostgreSQL"] = pgConnectionString;
}

// Configure services
builder.Services.AddSingleton<IProcessMonitorService, ProcessMonitorService>();
builder.Services.AddSingleton<IHWiNFOService, HWiNFOService>();

// Register offline storage service
builder.Services.AddSingleton<IOfflineStorageService, OfflineStorageService>();

// Register the actual database service implementation
builder.Services.AddSingleton<DatabaseService>();

// Register the offline-capable database service wrapper as the interface implementation
builder.Services.AddSingleton<IDatabaseService>(provider =>
{
    var databaseService = provider.GetRequiredService<DatabaseService>();
    var offlineStorageService = provider.GetRequiredService<IOfflineStorageService>();
    var logger = provider.GetRequiredService<ILogger<OfflineDatabaseService>>();
    return new OfflineDatabaseService(databaseService, offlineStorageService, logger);
});

builder.Services.AddHostedService<Worker>();

// Configure Windows Service
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Slov89.PCStats.Service";
});

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddEventLog(settings =>
{
    settings.SourceName = "Slov89.PCStats.Service";
});

var host = builder.Build();
host.Run();
