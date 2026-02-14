# Install Slov89.PCStats.Service
# Run this script as Administrator

param(
    [string]$InstallPath = "C:\Services\Slov89.PCStats.Service",
    [string]$ServiceName = "Slov89.PCStats.Service"
)

# Check if running as administrator
$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Error "This script must be run as Administrator"
    exit 1
}

Write-Host "Installing Slov89.PCStats.Service..." -ForegroundColor Cyan

# Check if service already exists and stop it before building
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existingService) {
    Write-Host "Service already exists. Stopping..." -ForegroundColor Yellow
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
}

# Kill any leftover PCStats processes that might be running outside service control
Write-Host "Checking for running PCStats processes..." -ForegroundColor Yellow
$runningProcesses = Get-Process "*PCStats*" -ErrorAction SilentlyContinue
if ($runningProcesses) {
    Write-Host "Found $($runningProcesses.Count) running PCStats process(es). Terminating..." -ForegroundColor Yellow
    $runningProcesses | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    Write-Host "PCStats processes terminated." -ForegroundColor Green
}

# Build and publish the service
Write-Host "Building service..." -ForegroundColor Yellow
Set-Location -Path "$PSScriptRoot"
dotnet publish -c Release -o $InstallPath

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed!"
    exit 1
}

Write-Host "Service built successfully" -ForegroundColor Green

# Remove existing service after successful build
if ($existingService) {
    Write-Host "Removing old service..." -ForegroundColor Yellow
    sc.exe delete $ServiceName
    Start-Sleep -Seconds 2
}

# Create the service
Write-Host "Creating Windows service..." -ForegroundColor Yellow
$binaryPath = Join-Path $InstallPath "Slov89.PCStats.Service.exe"
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
Write-Host "1. Set the environment variable 'slov89_pc_stats_utility_pg' with your PostgreSQL connection string" -ForegroundColor Yellow
Write-Host "   (Use Set-ConnectionString.ps1 in the root folder)" -ForegroundColor Yellow
Write-Host "2. Ensure PostgreSQL database is set up (see Database\README.md)" -ForegroundColor Yellow
Write-Host "3. Install and configure HWiNFO v8.14 with shared memory enabled" -ForegroundColor Yellow

Write-Host "`nStarting service..." -ForegroundColor Yellow
Start-Service -Name $ServiceName

Start-Sleep -Seconds 2
$status = Get-Service -Name $ServiceName

if ($status.Status -eq 'Running') {
    Write-Host "Service started successfully!" -ForegroundColor Green
} else {
    Write-Warning "Service did not start. Check Event Viewer for errors."
    Write-Host "Service status: $($status.Status)" -ForegroundColor Yellow
}

Write-Host "`nService management commands:" -ForegroundColor Cyan
Write-Host "  Start:   Start-Service $ServiceName" -ForegroundColor Gray
Write-Host "  Stop:    Stop-Service $ServiceName" -ForegroundColor Gray
Write-Host "  Status:  Get-Service $ServiceName" -ForegroundColor Gray
Write-Host "  Logs:    Check Event Viewer > Windows Logs > Application (Source: $ServiceName)" -ForegroundColor Gray
