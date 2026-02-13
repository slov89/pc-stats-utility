# Set the PostgreSQL connection string as a system environment variable
# This ensures the service can access it when running as a Windows Service

param(
    [Parameter(Mandatory=$true)]
    [string]$ConnectionString
)

# Set as system environment variable (requires admin privileges)
# This persists across reboots and is available to Windows services
[System.Environment]::SetEnvironmentVariable('slov89_pc_stats_utility_pg', $ConnectionString, [System.EnvironmentVariableTarget]::Machine)

Write-Host "Environment variable 'slov89_pc_stats_utility_pg' has been set at the Machine level." -ForegroundColor Green
Write-Host "Note: You may need to restart your IDE or service for the change to take effect." -ForegroundColor Yellow

# Also set for current process (for immediate use)
$env:slov89_pc_stats_utility_pg = $ConnectionString
Write-Host "Environment variable also set for current PowerShell session." -ForegroundColor Green
