#requires -Version 7
<#
.SYNOPSIS
    Build the Bishop.AI MSI installer.
.DESCRIPTION
    Publishes Bishop.Cli and Bishop.UI in Release configuration, then
    builds the Wix v5 installer project. Output MSI lands at
    installer\bin\Release\Bishop.AI.msi.
.PARAMETER Version
    The product version to embed in the MSI (e.g. 0.1.0). Defaults to 0.1.0.
.PARAMETER Configuration
    Build configuration (default Release).
#>
[CmdletBinding()]
param(
    [string] $Version = '0.1.0',
    [string] $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$InstallerDir = $PSScriptRoot
$RepoRoot     = Split-Path -Parent $InstallerDir
$PublishCli   = Join-Path $InstallerDir 'publish-cli'
$PublishUI    = Join-Path $InstallerDir 'publish-ui'

Write-Host "Cleaning previous publish outputs..." -ForegroundColor Cyan
if (Test-Path $PublishCli) { Remove-Item -Recurse -Force $PublishCli }
if (Test-Path $PublishUI)  { Remove-Item -Recurse -Force $PublishUI }

Write-Host "Publishing Bishop.Cli to $PublishCli..." -ForegroundColor Cyan
dotnet publish (Join-Path $RepoRoot 'src/Bishop.Cli/Bishop.Cli.csproj') `
    -c $Configuration -o $PublishCli --nologo

Write-Host "Publishing Bishop.UI to $PublishUI..." -ForegroundColor Cyan
dotnet publish (Join-Path $RepoRoot 'src/Bishop.UI/Bishop.UI.csproj') `
    -c $Configuration -o $PublishUI --nologo

Write-Host "Building MSI (version $Version)..." -ForegroundColor Cyan
dotnet build (Join-Path $InstallerDir 'Bishop.Installer.wixproj') `
    -c $Configuration -p:BishopVersion=$Version --nologo

$Msi = Join-Path $InstallerDir "bin/$Configuration/Bishop.AI.msi"
if (-not (Test-Path $Msi)) {
    Write-Error "Expected MSI at $Msi but it does not exist."
    exit 1
}

Write-Host "MSI built: $Msi" -ForegroundColor Green
