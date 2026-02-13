#Requires -RunAsAdministrator

<#
.SYNOPSIS
    Removes the Slov89.PCStats.Dashboard from IIS.

.DESCRIPTION
    This script stops and removes the IIS website and application pool.
    Optionally removes the deployed files.

.PARAMETER SiteName
    The name of the IIS website. Default: "Slov89.PCStats.Dashboard"

.PARAMETER AppPoolName
    The name of the IIS application pool. Default: "Slov89.PCStats.Dashboard"

.PARAMETER RemoveFiles
    If specified, also deletes the deployed files. Default: $false

.PARAMETER InstallPath
    The directory where the application is deployed. Default: "C:\inetpub\Slov89.PCStats.Dashboard"

.EXAMPLE
    .\Remove-IIS.ps1
    
.EXAMPLE
    .\Remove-IIS.ps1 -RemoveFiles
#>

param(
    [string]$SiteName = "Slov89.PCStats.Dashboard",
    [string]$AppPoolName = "Slov89.PCStats.Dashboard",
    [switch]$RemoveFiles,
    [string]$InstallPath = "C:\inetpub\Slov89.PCStats.Dashboard"
)

$ErrorActionPreference = "Stop"

# Import WebAdministration module
Import-Module WebAdministration -ErrorAction Stop

Write-Host "=======================================" -ForegroundColor Cyan
Write-Host "Slov89.PCStats.Dashboard - IIS Removal" -ForegroundColor Cyan
Write-Host "=======================================" -ForegroundColor Cyan
Write-Host ""

# Remove website
if (Get-Website -Name $SiteName -ErrorAction SilentlyContinue) {
    Write-Host "Stopping website '$SiteName'..." -ForegroundColor Yellow
    Stop-Website -Name $SiteName -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    
    Write-Host "Removing website '$SiteName'..." -ForegroundColor Yellow
    Remove-Website -Name $SiteName
    Write-Host "Website removed!" -ForegroundColor Green
} else {
    Write-Host "Website '$SiteName' not found." -ForegroundColor Gray
}

# Remove application pool
if (Test-Path "IIS:\AppPools\$AppPoolName") {
    Write-Host "Stopping application pool '$AppPoolName'..." -ForegroundColor Yellow
    Stop-WebAppPool -Name $AppPoolName -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    
    Write-Host "Removing application pool '$AppPoolName'..." -ForegroundColor Yellow
    Remove-WebAppPool -Name $AppPoolName
    Write-Host "Application pool removed!" -ForegroundColor Green
} else {
    Write-Host "Application pool '$AppPoolName' not found." -ForegroundColor Gray
}

# Remove files if requested
if ($RemoveFiles) {
    if (Test-Path $InstallPath) {
        Write-Host "Removing files from '$InstallPath'..." -ForegroundColor Yellow
        Remove-Item -Path $InstallPath -Recurse -Force
        Write-Host "Files removed!" -ForegroundColor Green
    } else {
        Write-Host "Install path '$InstallPath' not found." -ForegroundColor Gray
    }
} else {
    Write-Host ""
    Write-Host "Files at '$InstallPath' were not removed." -ForegroundColor Yellow
    Write-Host "Run with -RemoveFiles to delete them." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "=======================================" -ForegroundColor Green
Write-Host "Removal Complete!" -ForegroundColor Green
Write-Host "=======================================" -ForegroundColor Green
Write-Host ""
