using Slov89.PCStats.Dashboard.Components;
using Slov89.PCStats.Data;

var builder = WebApplication.CreateBuilder(args);

// When running under IIS, don't configure Kestrel URLs
builder.WebHost.UseUrls();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure circuit options for better reconnection handling
builder.Services.AddServerSideBlazor().AddCircuitOptions(options =>
{
    options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(5);
    options.DisconnectedCircuitMaxRetained = 200;
    options.JSInteropDefaultCallTimeout = TimeSpan.FromMinutes(2);
});

// Configure SignalR options for better reconnection during deployments
builder.Services.AddSignalR(options =>
{
    options.ClientTimeoutInterval = TimeSpan.FromMinutes(5);
    options.KeepAliveInterval = TimeSpan.FromSeconds(10);
    options.HandshakeTimeout = TimeSpan.FromMinutes(1);
    options.MaximumReceiveMessageSize = null;
});

// Register MetricsService
builder.Services.AddScoped<IMetricsService, MetricsService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
