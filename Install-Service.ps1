# Install PC Stats Monitoring Service
# Run this script as Administrator

param(
    [string]$InstallPath = "C:\Services\PCStatsService",
    [string]$ServiceName = "PCStatsMonitoringService"
)

# Check if running as administrator
$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Error "This script must be run as Administrator"
    exit 1
}

Write-Host "Installing PC Stats Monitoring Service..." -ForegroundColor Cyan

# Build and publish the service
Write-Host "Building service..." -ForegroundColor Yellow
Set-Location -Path "$PSScriptRoot\PCStatsService"
dotnet publish -c Release -o $InstallPath

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed!"
    exit 1
}

Write-Host "Service built successfully" -ForegroundColor Green

# Check if service already exists
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existingService) {
    Write-Host "Service already exists. Stopping and removing..." -ForegroundColor Yellow
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    sc.exe delete $ServiceName
    Start-Sleep -Seconds 2
}

# Create the service
Write-Host "Creating Windows service..." -ForegroundColor Yellow
$binaryPath = Join-Path $InstallPath "PCStatsService.exe"
sc.exe create $ServiceName binPath= $binaryPath start= auto

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to create service!"
    exit 1
}

# Set service description
sc.exe description $ServiceName "Monitors PC performance stats and logs to PostgreSQL database every 5 seconds"

# Configure service recovery options (restart on failure)
sc.exe failure $ServiceName reset= 86400 actions= restart/60000/restart/60000/restart/60000

Write-Host "`nService installed successfully!" -ForegroundColor Green
Write-Host "`nIMPORTANT: Before starting the service:" -ForegroundColor Cyan
Write-Host "1. Update the connection string in: $InstallPath\appsettings.json" -ForegroundColor Yellow
Write-Host "2. Ensure PostgreSQL database is set up (see Database\README.md)" -ForegroundColor Yellow
Write-Host "3. Install and configure HWiNFO v8.14 with shared memory enabled" -ForegroundColor Yellow

$startNow = Read-Host "`nDo you want to start the service now? (y/n)"
if ($startNow -eq 'y' -or $startNow -eq 'Y') {
    Write-Host "Starting service..." -ForegroundColor Yellow
    Start-Service -Name $ServiceName
    
    Start-Sleep -Seconds 2
    $status = Get-Service -Name $ServiceName
    
    if ($status.Status -eq 'Running') {
        Write-Host "Service started successfully!" -ForegroundColor Green
    } else {
        Write-Warning "Service did not start. Check Event Viewer for errors."
        Write-Host "Service status: $($status.Status)" -ForegroundColor Yellow
    }
}

Write-Host "`nService management commands:" -ForegroundColor Cyan
Write-Host "  Start:   Start-Service $ServiceName" -ForegroundColor Gray
Write-Host "  Stop:    Stop-Service $ServiceName" -ForegroundColor Gray
Write-Host "  Status:  Get-Service $ServiceName" -ForegroundColor Gray
Write-Host "  Logs:    Check Event Viewer > Windows Logs > Application (Source: $ServiceName)" -ForegroundColor Gray
