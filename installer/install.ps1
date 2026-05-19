#requires -Version 7
<#
.SYNOPSIS
    Publish Bishop.Cli and Bishop.UI from source and overlay them onto the
    locally installed Bishop.AI files (skipping the MSI).
.DESCRIPTION
    Use this during active development instead of installer\build.ps1 +
    MSI reinstall. Publishes Release output for the chosen components and
    copies it over %LOCALAPPDATA%\Programs\Bishop.AI\{Cli,UI}.

    The Bishop.AI MSI must have been installed at least once first so the
    target directories exist.
.PARAMETER Component
    Which component(s) to refresh: All (default), Cli, or UI.
.PARAMETER Configuration
    Build configuration (default Release).
.PARAMETER Force
    Stop any running Bishop.UI process before overwriting its files. Without
    this switch, the script aborts when the UI is running so you don't end
    up with a half-copied install.
#>
[CmdletBinding()]
param(
    [ValidateSet('All', 'Cli', 'UI')]
    [string] $Component = 'All',
    [string] $Configuration = 'Release',
    [switch] $Force
)

$ErrorActionPreference = 'Stop'

$InstallerDir = $PSScriptRoot
$RepoRoot     = Split-Path -Parent $InstallerDir
$InstallRoot  = Join-Path $env:LOCALAPPDATA 'Programs\Bishop.AI'
$CliInstall   = Join-Path $InstallRoot 'Cli'
$UiInstall    = Join-Path $InstallRoot 'UI'

if (-not (Test-Path $InstallRoot)) {
    throw "Install root $InstallRoot not found. Run installer\build.ps1 and install the MSI once before using install.ps1."
}

$doCli = $Component -in @('All', 'Cli')
$doUi  = $Component -in @('All', 'UI')

if ($doUi) {
    $running = Get-Process -Name 'Bishop.UI' -ErrorAction SilentlyContinue
    if ($running) {
        if ($Force) {
            Write-Host "Stopping Bishop.UI (pid $($running.Id))..." -ForegroundColor Yellow
            $running | Stop-Process -Force
            Start-Sleep -Milliseconds 500
        } else {
            throw "Bishop.UI is running (pid $($running.Id)). Close it or re-run with -Force."
        }
    }
}

function Publish-And-Overlay {
    param(
        [Parameter(Mandatory)] [string] $Project,
        [Parameter(Mandatory)] [string] $PublishDir,
        [Parameter(Mandatory)] [string] $InstallDir,
        [Parameter(Mandatory)] [string] $Label
    )

    if (Test-Path $PublishDir) { Remove-Item -Recurse -Force $PublishDir }

    Write-Host "Publishing $Label..." -ForegroundColor Cyan
    dotnet publish $Project -c $Configuration -o $PublishDir --nologo
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $Label" }

    if (-not (Test-Path $InstallDir)) {
        throw "Install dir $InstallDir not found. Run the MSI once first."
    }

    Write-Host "Overlaying $Label onto $InstallDir..." -ForegroundColor Cyan
    Copy-Item -Path (Join-Path $PublishDir '*') -Destination $InstallDir -Recurse -Force
}

if ($doCli) {
    Publish-And-Overlay `
        -Project (Join-Path $RepoRoot 'src/Bishop.Cli/Bishop.Cli.csproj') `
        -PublishDir (Join-Path $InstallerDir 'publish-cli') `
        -InstallDir $CliInstall `
        -Label 'Bishop.Cli'

    $cliExe = Join-Path $CliInstall 'bishop.exe'
    Write-Host "  bishop.exe -> $((Get-Item $cliExe).LastWriteTime)" -ForegroundColor Green
}

if ($doUi) {
    Publish-And-Overlay `
        -Project (Join-Path $RepoRoot 'src/Bishop.UI/Bishop.UI.csproj') `
        -PublishDir (Join-Path $InstallerDir 'publish-ui') `
        -InstallDir $UiInstall `
        -Label 'Bishop.UI'

    $uiExe = Join-Path $UiInstall 'Bishop.UI.exe'
    Write-Host "  Bishop.UI.exe -> $((Get-Item $uiExe).LastWriteTime)" -ForegroundColor Green
}

Write-Host "Done." -ForegroundColor Green
