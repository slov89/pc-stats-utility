#Requires -RunAsAdministrator

<#
.SYNOPSIS
    Builds and deploys the Slov89.PCStats.Dashboard to IIS.

.DESCRIPTION
    This script builds the dashboard in Release mode and deploys it to IIS.
    It creates an application pool and website if they don't exist.

.PARAMETER SiteName
    The name of the IIS website. Default: "Slov89.PCStats.Dashboard"

.PARAMETER AppPoolName
    The name of the IIS application pool. Default: "Slov89.PCStats.Dashboard"

.PARAMETER Port
    The HTTP port for the website. Default: 5000

.PARAMETER HttpsPort
    The HTTPS port for the website. Default: 5001

.PARAMETER InstallPath
    The directory where the application will be deployed. Default: "C:\inetpub\Slov89.PCStats.Dashboard"

.EXAMPLE
    .\Deploy-IIS.ps1
    
.EXAMPLE
    .\Deploy-IIS.ps1 -Port 8080 -HttpsPort 8443
#>

param(
    [string]$SiteName = "Slov89.PCStats.Dashboard",
    [string]$AppPoolName = "Slov89.PCStats.Dashboard",
    [int]$Port = 5000,
    [int]$HttpsPort = 5001,
    [string]$InstallPath = "C:\inetpub\Slov89.PCStats.Dashboard"
)

$ErrorActionPreference = "Stop"

# Import WebAdministration module
try {
    Import-Module WebAdministration -ErrorAction Stop
} catch {
    Write-Host "ERROR: Failed to load IIS WebAdministration module." -ForegroundColor Red
    Write-Host ""
    Write-Host "This usually means IIS is not installed or IIS Management Tools are not installed." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Please install IIS using Windows Features:" -ForegroundColor Yellow
    Write-Host "  1. Press Windows + R, type 'appwiz.cpl', press Enter" -ForegroundColor Cyan
    Write-Host "  2. Click 'Turn Windows features on or off'" -ForegroundColor Cyan
    Write-Host "  3. Enable Internet Information Services and IIS Management Console" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "After installing IIS, download and install the ASP.NET Core Hosting Bundle:" -ForegroundColor Yellow
    Write-Host "  https://dotnet.microsoft.com/download/dotnet/10.0" -ForegroundColor Cyan
    Write-Host ""
    exit 1
}

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "Slov89.PCStats.Dashboard - IIS Deploy" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# Get project directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = $scriptDir
$projectFile = Join-Path $projectDir "Slov89.PCStats.Dashboard.csproj"

if (-not (Test-Path $projectFile)) {
    Write-Error "Project file not found: $projectFile"
    exit 1
}

Write-Host "Project: $projectFile" -ForegroundColor Yellow
Write-Host "Install Path: $InstallPath" -ForegroundColor Yellow
Write-Host "Site Name: $SiteName" -ForegroundColor Yellow
Write-Host "App Pool: $AppPoolName" -ForegroundColor Yellow
Write-Host "HTTP Port: $Port" -ForegroundColor Yellow
Write-Host "HTTPS Port: $HttpsPort" -ForegroundColor Yellow
Write-Host ""

# Check if website exists and stop it
if (Get-Website -Name $SiteName -ErrorAction SilentlyContinue) {
    Write-Host "Stopping existing website..." -ForegroundColor Yellow
    Stop-Website -Name $SiteName
    Start-Sleep -Seconds 2
}

# Check if app pool exists and stop it
if (Test-Path "IIS:\AppPools\$AppPoolName") {
    Write-Host "Stopping existing application pool..." -ForegroundColor Yellow
    Stop-WebAppPool -Name $AppPoolName
    Start-Sleep -Seconds 2
}

# Build and publish
Write-Host "Building project (Release)..." -ForegroundColor Cyan
dotnet publish $projectFile -c Release -o $InstallPath --nologo
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed with exit code $LASTEXITCODE"
    exit 1
}
Write-Host "Build completed successfully!" -ForegroundColor Green
Write-Host ""

# Create application pool if it doesn't exist
if (-not (Test-Path "IIS:\AppPools\$AppPoolName")) {
    Write-Host "Creating application pool '$AppPoolName'..." -ForegroundColor Cyan
    New-WebAppPool -Name $AppPoolName
    
    # Configure app pool for .NET (No Managed Code for .NET Core/10)
    Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name managedRuntimeVersion -Value ""
    Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name processModel.identityType -Value "ApplicationPoolIdentity"
    
    Write-Host "Application pool created!" -ForegroundColor Green
} else {
    Write-Host "Application pool '$AppPoolName' already exists." -ForegroundColor Yellow
}

# Create website if it doesn't exist
if (-not (Get-Website -Name $SiteName -ErrorAction SilentlyContinue)) {
    Write-Host "Creating website '$SiteName'..." -ForegroundColor Cyan
    
    New-Website -Name $SiteName `
                -ApplicationPool $AppPoolName `
                -PhysicalPath $InstallPath `
                -Port $Port
    
    # Add HTTPS binding if different from HTTP
    if ($HttpsPort -ne $Port) {
        Write-Host "Adding HTTPS binding on port $HttpsPort..." -ForegroundColor Cyan
        New-WebBinding -Name $SiteName -Protocol https -Port $HttpsPort
    }
    
    Write-Host "Website created!" -ForegroundColor Green
} else {
    Write-Host "Website '$SiteName' already exists." -ForegroundColor Yellow
    
    # Update physical path in case it changed
    Set-ItemProperty "IIS:\Sites\$SiteName" -Name physicalPath -Value $InstallPath
}

# Ensure the app pool has access to the environment variable
Write-Host ""
Write-Host "Note: Ensure the application pool identity has access to the environment variable:" -ForegroundColor Yellow
Write-Host "  slov89_pc_stats_utility_pg" -ForegroundColor Cyan
Write-Host ""
Write-Host "If using ApplicationPoolIdentity, you may need to:" -ForegroundColor Yellow
Write-Host "  1. Set the environment variable at Machine level (not User)" -ForegroundColor Cyan
Write-Host "  2. Or change the app pool identity to a specific user account" -ForegroundColor Cyan
Write-Host ""

# Start application pool
Write-Host "Starting application pool..." -ForegroundColor Cyan
Start-WebAppPool -Name $AppPoolName
Start-Sleep -Seconds 2

# Start website
Write-Host "Starting website..." -ForegroundColor Cyan
Start-Website -Name $SiteName
Start-Sleep -Seconds 2

# Configure web.config for better error logging
$webConfigPath = Join-Path $InstallPath "web.config"
if (Test-Path $webConfigPath) {
    Write-Host "Configuring web.config for debugging..." -ForegroundColor Cyan
    try {
        [xml]$webConfig = Get-Content $webConfigPath
        $aspNetCore = $webConfig.configuration.location.'system.webServer'.aspNetCore
        if ($aspNetCore) {
            $aspNetCore.SetAttribute("stdoutLogEnabled", "true")
            $aspNetCore.SetAttribute("hostingModel", "inprocess")
            # Ensure DLL argument is set correctly
            $aspNetCore.SetAttribute("arguments", ".\Slov89.PCStats.Dashboard.dll")
            $webConfig.Save($webConfigPath)
            Write-Host "Updated web.config for debugging." -ForegroundColor Green
            
            # Restart app pool to pick up changes
            Stop-WebAppPool -Name $AppPoolName -ErrorAction SilentlyContinue
            Start-Sleep -Seconds 2
            Start-WebAppPool -Name $AppPoolName
            Write-Host "Restarted application pool." -ForegroundColor Green
        }
    } catch {
        Write-Host "Warning: Could not update web.config - $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

# Check status
try {
    $site = Get-Website -Name $SiteName -ErrorAction Stop
    $appPool = Get-IISAppPool -Name $AppPoolName -ErrorAction Stop
} catch {
    # Fallback to checking if services are running via alternative method
    Write-Host "Note: Unable to verify status using IIS cmdlets." -ForegroundColor Yellow
    Write-Host "Check IIS Manager to verify the site is running." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "=====================================" -ForegroundColor Green
    Write-Host "Deployment Complete!" -ForegroundColor Green  
    Write-Host "=====================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "URLs:" -ForegroundColor Cyan
    Write-Host "  http://localhost:$Port" -ForegroundColor Yellow
    if ($HttpsPort -ne $Port) {
        Write-Host "  https://localhost:$HttpsPort (requires SSL certificate)" -ForegroundColor Yellow
    }
    Write-Host ""
    Write-Host "Physical Path: $InstallPath" -ForegroundColor Cyan
    Write-Host ""
    return
}

Write-Host ""
Write-Host "=====================================" -ForegroundColor Green
Write-Host "Deployment Complete!" -ForegroundColor Green
Write-Host "=====================================" -ForegroundColor Green
Write-Host ""
Write-Host "Website Status: " -NoNewline
if ($site.State -eq "Started") {
    Write-Host $site.State -ForegroundColor Green
} else {
    Write-Host $site.State -ForegroundColor Red
}

Write-Host "App Pool Status: " -NoNewline
if ($appPool.State -eq "Started") {
    Write-Host $appPool.State -ForegroundColor Green
} else {
    Write-Host $appPool.State -ForegroundColor Red
}

Write-Host ""
Write-Host "URLs:" -ForegroundColor Cyan
Write-Host "  http://localhost:$Port" -ForegroundColor Yellow
if ($HttpsPort -ne $Port) {
    Write-Host "  https://localhost:$HttpsPort (requires SSL certificate)" -ForegroundColor Yellow
}
Write-Host ""
Write-Host "Physical Path: $InstallPath" -ForegroundColor Cyan
Write-Host ""

# Check for common issues
Write-Host "Troubleshooting:" -ForegroundColor Cyan
Write-Host "  - Check Event Viewer Application logs for errors" -ForegroundColor Gray
Write-Host "  - Verify ASP.NET Core Hosting Bundle is installed" -ForegroundColor Gray
Write-Host "  - Ensure environment variable is set at Machine level" -ForegroundColor Gray
Write-Host "  - Check IIS logs: C:\inetpub\logs\LogFiles" -ForegroundColor Gray
Write-Host ""
