#!/usr/bin/env pwsh
# Runs the test suite with coverlet collector and renders a coverage report.
# Output: TestResults/coverage-report/index.html and Summary.json.

$ErrorActionPreference = 'Stop'

$repoRoot   = $PSScriptRoot
$testProj   = Join-Path $repoRoot 'bishop/tests/Bishop.Tests/Bishop.Tests.csproj'
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

# Emit normalized summary consumed by /bish-coverage. Schema (stack-agnostic):
#   { schemaVersion, threshold, generatedAt, modules: [{ name, file, lineCoverage, linesCoverable, cyclomaticComplexity, crapScore }] }
$cob = Get-ChildItem $resultsDir -Recurse -Filter coverage.cobertura.xml | Select-Object -First 1
if ($null -eq $cob) {
    Write-Warning "coverage.cobertura.xml not found; skipping normalized summary."
} else {
    [xml]$xml = Get-Content $cob.FullName
    $rootForward = ($repoRoot -replace '\\', '/').TrimEnd('/') + '/'
    $modules = foreach ($pkg in $xml.coverage.packages.package) {
        foreach ($cls in $pkg.classes.class) {
            $rel = ($cls.filename -replace '\\', '/')
            if ($rel.StartsWith($rootForward, [StringComparison]::OrdinalIgnoreCase)) {
                $rel = $rel.Substring($rootForward.Length)
            }
            $lineRate   = [double]$cls.'line-rate'
            $complexity = [int]$cls.complexity
            $crap       = ($complexity * $complexity) * [Math]::Pow(1 - $lineRate, 3) + $complexity
            [PSCustomObject]@{
                name                 = $cls.name
                file                 = $rel
                lineCoverage         = [Math]::Round($lineRate * 100, 2)
                linesCoverable       = @($cls.lines.line).Count
                cyclomaticComplexity = $complexity
                crapScore            = [Math]::Round($crap, 1)
            }
        }
    }
    $summary = [PSCustomObject]@{
        schemaVersion = 1
        threshold     = 80
        generatedAt   = (Get-Date).ToString('o')
        modules       = $modules
    }
    $summaryPath = Join-Path $resultsDir 'coverage-summary.json'
    $summary | ConvertTo-Json -Depth 5 | Set-Content $summaryPath -Encoding UTF8
}

Write-Host ""
Write-Host "Coverage report:    $reportDir/index.html"
Write-Host "ReportGen summary:  $reportDir/Summary.json"
Write-Host "Bishop summary:     $resultsDir/coverage-summary.json"
