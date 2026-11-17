<#
.SYNOPSIS
    Starts OppsView on Windows, each service in its own PowerShell window.

.DESCRIPTION
    Opens one window per service and then the website in your browser.
    Close a window (or press Ctrl+C in it) to stop that service.
    PostgreSQL runs by itself as a Windows service, and RabbitMQ lives on
    CloudAMQP, so neither is started here.

.EXAMPLE
    .\start.ps1                  # API + website (tracker, Gmail import, Insights)
.EXAMPLE
    .\start.ps1 -All             # everything: + matcher and notifier
.EXAMPLE
    .\start.ps1 -Matcher         # API + website + CV matcher
.EXAMPLE
    .\start.ps1 -Notifier        # API + website + reminders
#>
param(
    [switch]$Matcher,
    [switch]$Notifier,
    [switch]$All
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

if ($All) { $Matcher = $true; $Notifier = $true }

# Opens a new PowerShell window in $Folder, titled "OppsView <Title>", that
# runs $Command and stays open afterwards so you can read any error.
function Open-ServiceWindow([string]$Title, [string]$Folder, [string]$Command) {
    $script = "`$host.UI.RawUI.WindowTitle = 'OppsView $Title'; $Command"
    Start-Process powershell `
        -WorkingDirectory (Join-Path $root $Folder) `
        -ArgumentList "-NoExit -ExecutionPolicy Bypass -Command `"$script`""
    Write-Host "  started $Title"
}

Write-Host 'Starting OppsView...'

Open-ServiceWindow 'API' 'api' 'dotnet run --project src/OppsView.Api'

# First run on this machine: install the website's packages before starting.
$webCommand = 'npm run dev'
if (-not (Test-Path (Join-Path $root 'web\node_modules'))) { $webCommand = "npm install; $webCommand" }
Open-ServiceWindow 'website' 'web' $webCommand

if ($Matcher) {
    if (Test-Path (Join-Path $root 'matcher\.venv\Scripts\uvicorn.exe')) {
        # Runs uvicorn from the virtual environment directly: no need to activate it.
        Open-ServiceWindow 'matcher' 'matcher' '.\.venv\Scripts\uvicorn.exe matcher.api:app --port 8000'
    } else {
        Write-Warning 'Matcher skipped: matcher\.venv is missing. Set it up once (README step 7), then run this again.'
    }
}

if ($Notifier) {
    if (Test-Path (Join-Path $root 'notifier\config\local.properties')) {
        Open-ServiceWindow 'notifier' 'notifier' '.\mvnw.cmd spring-boot:run'
    } else {
        Write-Warning 'Notifier skipped: notifier\config\local.properties is missing. See notifier\README.md.'
    }
}

# Give Vite a moment to start, then open the website.
Start-Sleep -Seconds 5
Start-Process 'http://localhost:5173'
Write-Host 'Done. The API takes a few more seconds; refresh the page if it says it cannot reach it.'
