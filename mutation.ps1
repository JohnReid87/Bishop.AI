#!/usr/bin/env pwsh
# Runs Stryker.NET mutation testing scoped to a project or file glob.
# Output: StrykerOutput/<timestamp>/mutation-report.html  and  mutation-report.json
#
# Fast path (~5-7 min):
#   ./mutation.ps1 -Project Bishop.Game
#   ./mutation.ps1 -Project Bishop.App -Mutate "src/Bishop.App/Handlers/**"
#
# Slow path (~2-4h, all source projects):
#   ./mutation.ps1 -All

param (
    [string]$Project,
    [string]$Mutate,
    [switch]$All
)

$ErrorActionPreference = 'Stop'

if (-not $All -and -not $Project -and -not $Mutate) {
    Write-Error "Specify -Project <name> and/or -Mutate <glob> for a scoped run, or -All for solution-wide (~2-4h).`n  Example: ./mutation.ps1 -Project Bishop.Game"
    exit 1
}

$repoRoot = $PSScriptRoot

dotnet tool restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$strykerArgs = @()

if ($Project) {
    $projPath = Join-Path $repoRoot 'src' $Project "$Project.csproj"
    if (-not (Test-Path $projPath)) {
        Write-Error "Project not found: $projPath"
        exit 1
    }
    $strykerArgs += '--project', $projPath
}

if ($Mutate) {
    $strykerArgs += '--mutate', $Mutate
}

if ($All) {
    Write-Host "Running solution-wide mutation test — estimated 2-4h..."
}

Push-Location $repoRoot
try {
    dotnet tool run dotnet-stryker @strykerArgs
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
} finally {
    Pop-Location
}

Write-Host ""
Write-Host "Mutation report: $(Join-Path $repoRoot 'StrykerOutput')"
