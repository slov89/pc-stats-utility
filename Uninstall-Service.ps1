# Uninstall PC Stats Monitoring Service
# Run this script as Administrator

param(
    [string]$ServiceName = "PCStatsMonitoringService",
    [string]$InstallPath = "C:\Services\PCStatsService",
    [switch]$RemoveFiles
)

# Check if running as administrator
$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Error "This script must be run as Administrator"
    exit 1
}

Write-Host "Uninstalling PC Stats Monitoring Service..." -ForegroundColor Cyan

# Check if service exists
$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if (-not $service) {
    Write-Warning "Service '$ServiceName' not found"
} else {
    # Stop the service if running
    if ($service.Status -eq 'Running') {
        Write-Host "Stopping service..." -ForegroundColor Yellow
        Stop-Service -Name $ServiceName -Force
        Start-Sleep -Seconds 2
    }
    
    # Delete the service
    Write-Host "Removing service..." -ForegroundColor Yellow
    sc.exe delete $ServiceName
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Service removed successfully" -ForegroundColor Green
    } else {
        Write-Error "Failed to remove service"
        exit 1
    }
}

# Remove files if requested
if ($RemoveFiles) {
    if (Test-Path $InstallPath) {
        Write-Host "Removing installation files from $InstallPath..." -ForegroundColor Yellow
        Remove-Item -Path $InstallPath -Recurse -Force
        Write-Host "Files removed successfully" -ForegroundColor Green
    } else {
        Write-Warning "Installation path not found: $InstallPath"
    }
}

Write-Host "`nUninstallation complete!" -ForegroundColor Green
Write-Host "`nNote: Database data has NOT been removed." -ForegroundColor Yellow
Write-Host "To remove database data, run: DROP DATABASE pc_stats_monitoring;" -ForegroundColor Gray
