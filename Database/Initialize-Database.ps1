# Initialize-Database.ps1
# Description: Executes the 01_InitialSchema.sql script against a PostgreSQL database
# Usage: .\Initialize-Database.ps1 -Server "localhost" -Port 5432 -Database "pcstats" -Username "postgres" -Password (ConvertTo-SecureString "yourpassword" -AsPlainText -Force)

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$Server,
    
    [Parameter(Mandatory=$false)]
    [int]$Port = 5432,
    
    [Parameter(Mandatory=$true)]
    [string]$Database,
    
    [Parameter(Mandatory=$true)]
    [string]$Username,
    
    [Parameter(Mandatory=$true)]
    [SecureString]$Password
)

# Get the directory where this script is located
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$SqlFile = Join-Path $ScriptDir "01_InitialSchema.sql"

Write-Host "======================================" -ForegroundColor Cyan
Write-Host "PC Stats Database Initialization" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

# Validate SQL file exists
if (-not (Test-Path $SqlFile)) {
    Write-Host "ERROR: SQL file not found at: $SqlFile" -ForegroundColor Red
    exit 1
}

Write-Host "SQL File: $SqlFile" -ForegroundColor Gray
Write-Host "Server: $Server" -ForegroundColor Gray
Write-Host "Port: $Port" -ForegroundColor Gray
Write-Host "Database: $Database" -ForegroundColor Gray
Write-Host "Username: $Username" -ForegroundColor Gray
Write-Host ""

# Check if psql is available
$psqlCommand = Get-Command psql -ErrorAction SilentlyContinue
if (-not $psqlCommand) {
    Write-Host "ERROR: psql command not found in PATH" -ForegroundColor Red
    Write-Host "Please install PostgreSQL or add the PostgreSQL bin directory to your PATH" -ForegroundColor Yellow
    Write-Host "Common locations:" -ForegroundColor Yellow
    Write-Host "  - C:\Program Files\PostgreSQL\16\bin" -ForegroundColor Yellow
    Write-Host "  - C:\Program Files\PostgreSQL\15\bin" -ForegroundColor Yellow
    exit 1
}

Write-Host "Found psql at: $($psqlCommand.Source)" -ForegroundColor Green
Write-Host ""

# Convert SecureString to plain text for PGPASSWORD environment variable
$BSTR = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($Password)
$PlainPassword = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($BSTR)
[System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($BSTR)

# Set PGPASSWORD environment variable for non-interactive authentication
$env:PGPASSWORD = $PlainPassword

try {
    # First, check if database exists and create if needed
    Write-Host "Checking if database '$Database' exists..." -ForegroundColor Cyan
    $dbCheckQuery = "SELECT 1 FROM pg_database WHERE datname = '$Database';"
    $dbExists = & psql -h $Server -p $Port -U $Username -d postgres -t -c $dbCheckQuery 2>&1
    
    if ($dbExists -match "1") {
        Write-Host "Database '$Database' already exists." -ForegroundColor Green
    } else {
        Write-Host "Database '$Database' does not exist. Creating..." -ForegroundColor Yellow
        $createResult = & psql -h $Server -p $Port -U $Username -d postgres -c "CREATE DATABASE $Database;" 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Database '$Database' created successfully!" -ForegroundColor Green
        } else {
            Write-Host "Failed to create database:" -ForegroundColor Red
            Write-Host $createResult -ForegroundColor Red
            exit 1
        }
    }
    Write-Host ""
    
    Write-Host "Executing SQL script..." -ForegroundColor Cyan
    Write-Host ""
    
    # Execute the SQL script using psql
    $output = & psql -h $Server -p $Port -U $Username -d $Database -f $SqlFile 2>&1
    
    # Check exit code
    if ($LASTEXITCODE -eq 0) {
        Write-Host $output
        Write-Host ""
        Write-Host "======================================" -ForegroundColor Green
        Write-Host "Database initialization completed successfully!" -ForegroundColor Green
        Write-Host "======================================" -ForegroundColor Green
        Write-Host ""
        Write-Host "Next steps:" -ForegroundColor Cyan
        Write-Host "1. Update the connection string in PCStatsService\appsettings.json" -ForegroundColor White
        Write-Host "2. Verify your PostgreSQL password is correct in the connection string" -ForegroundColor White
        Write-Host "3. Install HWiNFO v8.14 and enable shared memory support" -ForegroundColor White
        Write-Host "4. Run Install-Service.ps1 to deploy the Windows service" -ForegroundColor White
    } else {
        Write-Host $output -ForegroundColor Red
        Write-Host ""
        Write-Host "======================================" -ForegroundColor Red
        Write-Host "Database initialization FAILED!" -ForegroundColor Red
        Write-Host "======================================" -ForegroundColor Red
        Write-Host ""
        Write-Host "Please check the error messages above and verify:" -ForegroundColor Yellow
        Write-Host "- Username '$Username' has appropriate permissions" -ForegroundColor Yellow
        Write-Host "- Server '$Server' is accessible on port $Port" -ForegroundColor Yellow
        Write-Host "- pg_hba.conf on the server allows connections from this client" -ForegroundColor Yellow
        exit 1
    }
} finally {
    # Clear password from memory and environment
    $PlainPassword = $null
    $env:PGPASSWORD = $null
}
