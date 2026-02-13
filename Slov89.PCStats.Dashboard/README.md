# Slov89.PCStats.Dashboard

Blazor Server web application for real-time visualization of PC performance metrics.

## Overview

Interactive web dashboard providing:
- **Real-time charts** of system CPU and memory usage
- **CPU temperature graphs** (if HWiNFO data available)
- **Top processes tracking** with historical CPU usage
- **Time range filtering** (5, 10, 30, 60 minutes)
- **Summary statistics** (averages, peaks, data points)

## Features

### Metrics Page

The main dashboard provides interactive visualization:

#### System Metrics
- **CPU Usage Chart** - Area chart showing total system CPU percentage over time
- **Memory Usage Chart** - Area chart displaying memory consumption in MB

#### CPU Temperatures
- **Temperature Charts** - Line charts for:
  - CPU Tctl/Tdie (main temperature sensor)
  - CPU Die Average
  - CCD1/CCD2 die temperatures (if available)

#### Process Analysis
- **Top Processes Chart** - Multi-line chart showing CPU usage over time for top 5 processes by average CPU

#### Interactive Controls
- **Time Range Selector** - Buttons for 5, 10, 30, or 60 minutes (default: 10 minutes)
- **Refresh Button** - Manually update all charts
- **Summary Stats** - Display average CPU, peak CPU, average memory, data point count

#### Chart Features
- Hover over data points for details
- Zoom and pan capabilities
- Responsive design (mobile-friendly)
- Smooth animations

## Running the Dashboard

### Prerequisites
- .NET 10 Runtime
- PostgreSQL with monitoring data (populated by Slov89.PCStats.Service)
- Environment variable `slov89_pc_stats_utility_pg` set

### Development Mode

```powershell
cd Slov89.PCStats.Dashboard
dotnet run
```

Navigate to: `https://localhost:5001` (or URL shown in console)

### Production Deployment

#### Option 1: IIS (Recommended)

**Automated Deployment:**

```powershell
cd Slov89.PCStats.Dashboard
.\Deploy-IIS.ps1
```

This builds in Release mode and deploys to IIS at `http://localhost:5000`.

**Custom Configuration:**

```powershell
.\Deploy-IIS.ps1 -Port 8080 -HttpsPort 8443 -InstallPath "D:\WebApps\Dashboard"
```

**Prerequisites:**
- ASP.NET Core Hosting Bundle installed
- IIS with .NET support enabled
- Machine-level environment variable `slov89_pc_stats_utility_pg` set
- Run PowerShell as Administrator

**Removal:**

```powershell
.\Remove-IIS.ps1            # Removes IIS config only
.\Remove-IIS.ps1 -RemoveFiles  # Also deletes deployed files
```

#### Option 2: Kestrel (Self-Hosted)

```powershell
# Publish
dotnet publish -c Release -o C:\inetpub\Slov89.PCStats.Dashboard

# Run
cd C:\inetpub\Slov89.PCStats.Dashboard
dotnet Slov89.PCStats.Dashboard.dll --urls "http://*:5000;https://*:5001"
```

#### Option 3: Windows Service

Use `sc.exe` or NSSM to run the dashboard as a Windows service.

## Configuration

### Environment Variable (Required)

The dashboard reads the database connection string from an environment variable:

```powershell
.\Set-ConnectionString.ps1 -ConnectionString "Host=localhost;Port=5432;Database=pcstats;Username=postgres;Password=YOUR_PASSWORD"
```

This sets `slov89_pc_stats_utility_pg`.

### appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

**Note:** Connection string is NOT in config files (security best practice).

### appsettings.Development.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

## Architecture

### Components

#### Pages
- **Home.razor** - Landing page
- **Metrics.razor** - Main metrics visualization page

#### Layout
- **MainLayout.razor** - Application layout
- **NavMenu.razor** - Navigation sidebar

#### Services
Uses `IMetricsService` from `Slov89.PCStats.Data` for database queries.

### Data Flow

1. User selects time range (5/10/30/60 minutes)
2. Metrics page calls `IMetricsService` methods
3. Service queries PostgreSQL for time-filtered data
4. Data transformed for ApexCharts
5. Interactive charts rendered
6. User can refresh or change time range

## Dependencies

### NuGet Packages
- `Blazor-ApexCharts` (4.0.0) - Charting library
- `Npgsql` (10.0.1) - PostgreSQL driver (via referenced projects)

### Project References
- `Slov89.PCStats.Data` - Metrics queries
- `Slov89.PCStats.Models` - Data models (via Data)

### Frontend Libraries
- **Bootstrap 5** - UI framework
- **ApexCharts.js** - JavaScript charting (wrapped by Blazor-ApexCharts)
- **Bootstrap Icons** - Icon font

## Development

### Hot Reload

Blazor Server supports hot reload:
```powershell
dotnet watch run
```

Changes to Razor files reload automatically.

### Debug Mode

Set breakpoints in `.razor` files code blocks or in service classes.

Run with debugger attached (F5 in Visual Studio / VS Code).

## Customization

### Adding Charts

1. Add query method to `IMetricsService` / `MetricsService`
2. Add chart component to `Metrics.razor`
3. Configure `ApexChartOptions` for styling
4. Wire up data binding

### Changing Time Ranges

Edit button values in `Metrics.razor`:
```razor
<button @onclick="() => SelectTimeRange(15)">15 Minutes</button>
```

### Styling

- Modify `app.css` for global styles
- Edit Bootstrap theme in `_Imports.razor` or layout
- Customize ApexCharts theme in chart options

## Troubleshooting

### Charts Not Loading
- Verify PostgreSQL connection: Check environment variable
- Ensure monitoring service is running and collecting data
- Check browser console for JavaScript errors
- Verify data exists for selected time range

### No Data Displayed
- Check if `Slov89.PCStats.Service` is running
- Verify database has recent snapshots: `SELECT COUNT(*) FROM snapshots WHERE snapshot_timestamp > NOW() - INTERVAL '1 hour';`
- Check time range selection (may need longer range)

### Slow Performance
- Database queries are optimized, but very large time ranges may be slow
- Consider adding database indexes if custom queries added
- Check PostgreSQL query performance

### Connection Errors
- Verify environment variable is set at Machine level
- Restart the application after setting environment variable
- Check PostgreSQL is running and accessible

## URLs

- **Development**: `https://localhost:5001`
- **Production**: Configure in hosting setup

## Target Framework

- .NET 10.0
- Blazor Server (Interactive Server Components)
