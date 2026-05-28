#requires -Version 7
<#
.SYNOPSIS
    Build and deploy Bishop.Cli and Bishop.UI, register on PATH, and install skills.
.DESCRIPTION
    Works for both fresh installs and updates. Publishes Release output for the
    chosen components, copies to %LOCALAPPDATA%\Programs\Bishop.AI\{Cli,UI},
    adds the Cli directory to the user PATH, and runs bishop install-skills.
.PARAMETER Component
    Which component(s) to deploy: All (default), Cli, or UI.
.PARAMETER Configuration
    Build configuration (default Release).
#>
[CmdletBinding()]
param(
    [ValidateSet('All', 'Cli', 'UI')]
    [string] $Component = 'All',
    [string] $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$RepoRoot    = $PSScriptRoot
$InstallRoot = Join-Path $env:LOCALAPPDATA 'Programs\Bishop.AI'
$CliInstall  = Join-Path $InstallRoot 'Cli'
$UiInstall   = Join-Path $InstallRoot 'UI'
$TempBase    = Join-Path $env:TEMP 'Bishop.AI.Publish'

$doCli = $Component -in @('All', 'Cli')
$doUi  = $Component -in @('All', 'UI')

# Stop Bishop.UI before overwriting its files
if ($doUi) {
    $running = Get-Process -Name 'Bishop.UI' -ErrorAction SilentlyContinue
    if ($running) {
        Write-Host "Stopping Bishop.UI (pid $($running.Id))..." -ForegroundColor Yellow
        $running | Stop-Process -Force
        Start-Sleep -Milliseconds 500
    }
}

function Publish-And-Deploy {
    param(
        [string] $Project,
        [string] $InstallDir,
        [string] $Label
    )

    $PublishDir = Join-Path $TempBase $Label
    if (Test-Path $PublishDir) { Remove-Item -Recurse -Force $PublishDir }

    Write-Host "Publishing $Label..." -ForegroundColor Cyan
    dotnet publish $Project -c $Configuration -o $PublishDir --nologo
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $Label" }

    if (-not (Test-Path $InstallDir)) {
        New-Item -ItemType Directory -Path $InstallDir | Out-Null
        Write-Host "  Created $InstallDir" -ForegroundColor Gray
    }

    Write-Host "  Deploying to $InstallDir..." -ForegroundColor Cyan
    Copy-Item -Path (Join-Path $PublishDir '*') -Destination $InstallDir -Recurse -Force
}

if ($doCli) {
    Publish-And-Deploy `
        -Project (Join-Path $RepoRoot 'src\Bishop.Cli\Bishop.Cli.csproj') `
        -InstallDir $CliInstall `
        -Label 'Cli'
}

if ($doUi) {
    Publish-And-Deploy `
        -Project (Join-Path $RepoRoot 'src\Bishop.UI\Bishop.UI.csproj') `
        -InstallDir $UiInstall `
        -Label 'UI'
}

# Add Cli to the user PATH if not already present
$regPath      = 'HKCU:\Environment'
$existingPath = (Get-ItemProperty -Path $regPath -Name PATH -ErrorAction SilentlyContinue).PATH
if ($existingPath -notlike "*$CliInstall*") {
    $newPath = if ($existingPath) { "$existingPath;$CliInstall" } else { $CliInstall }
    Set-ItemProperty -Path $regPath -Name PATH -Value $newPath
    Write-Host "Added $CliInstall to user PATH (restart terminal to take effect)" -ForegroundColor Yellow
}

# Install skills using the freshly deployed binary
if ($doCli) {
    Write-Host "Installing skills..." -ForegroundColor Cyan
    Push-Location $RepoRoot
    try {
        & (Join-Path $CliInstall 'bishop.exe') install-skills
        if ($LASTEXITCODE -ne 0) { throw "install-skills failed" }
    } finally {
        Pop-Location
    }
}

Write-Host "Done." -ForegroundColor Green
