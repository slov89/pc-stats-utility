using Slov89.PCStats.Dashboard.Components;
using Slov89.PCStats.Data;

var builder = WebApplication.CreateBuilder(args);

// When running under IIS, don't configure Kestrel URLs
builder.WebHost.UseUrls();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

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
