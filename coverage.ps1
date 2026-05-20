#!/usr/bin/env pwsh
# Runs the test suite with coverlet collector and renders a coverage report.
# Output: TestResults/coverage-report/index.html and Summary.json.

$ErrorActionPreference = 'Stop'

$repoRoot   = $PSScriptRoot
$testProj   = Join-Path $repoRoot 'tests/Bishop.Tests/Bishop.Tests.csproj'
$settings   = Join-Path $repoRoot 'coverlet.runsettings'
$resultsDir = Join-Path $repoRoot 'TestResults'
$reportDir  = Join-Path $resultsDir 'coverage-report'

if (Test-Path $resultsDir) {
    Remove-Item $resultsDir -Recurse -Force
}

dotnet tool restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet test $testProj `
    --collect:"XPlat Code Coverage" `
    --settings $settings `
    --results-directory $resultsDir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet reportgenerator `
    "-reports:$resultsDir/**/coverage.cobertura.xml" `
    "-targetdir:$reportDir" `
    "-reporttypes:Html;JsonSummary"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host ""
Write-Host "Coverage report: $reportDir/index.html"
Write-Host "Summary JSON:    $reportDir/Summary.json"
