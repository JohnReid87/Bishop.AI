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

    # Stryker's mutate globs are resolved relative to the project being mutated,
    # not the repo root. The repo-level stryker-config.json uses `src/**/*.cs`
    # (root-relative) which silently filters out every mutant for a `--project`
    # run. Default to `**/*.cs` (project-relative) so `-Project Foo` always
    # mutates Foo. Explicit `-Mutate` still wins.
    if (-not $Mutate) {
        $Mutate = '**/*.cs'
    }
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

# Emit normalised summary consumed by /bish-tests.
# Schema (stack-agnostic, mirrors coverage-summary.json):
#   { schemaVersion, threshold, generatedAt,
#     modules: [{ name, file, mutationScore, mutantsCovered, survived: [{ line, mutator, original, replacement }] }] }
$reportJson = Get-ChildItem (Join-Path $repoRoot 'StrykerOutput') -Recurse -Filter 'mutation-report.json' |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if ($null -eq $reportJson) {
    Write-Warning "mutation-report.json not found; skipping normalised summary."
} else {
    $raw = Get-Content $reportJson.FullName -Raw | ConvertFrom-Json
    $rootForward = ($repoRoot -replace '\\', '/').TrimEnd('/') + '/'

    $modules = foreach ($fileKey in $raw.files.PSObject.Properties.Name) {
        $fileEntry = $raw.files.PSObject.Properties[$fileKey].Value

        $relPath = $fileKey -replace '\\', '/'
        if ($relPath.StartsWith($rootForward, [System.StringComparison]::OrdinalIgnoreCase)) {
            $relPath = $relPath.Substring($rootForward.Length)
        }

        $name = ($fileKey -split '[/\\]')[-1] -replace '\.[^.]+$', ''

        $sourceLines = $fileEntry.source -split "`r?`n"

        $mutants  = @($fileEntry.mutants)
        $killed   = @($mutants | Where-Object { $_.status -eq 'Killed' -or $_.status -eq 'Timeout' })
        # Mutants annotated with `// Stryker disable once` carry status 'Ignored' and are excluded here automatically.
        $survived = @($mutants | Where-Object { $_.status -eq 'Survived' })
        $covered  = $killed.Count + $survived.Count

        $score = if ($covered -gt 0) { [double]("{0:F2}" -f ($killed.Count / $covered * 100)) } else { 0.0 }

        $survivedList = foreach ($m in $survived) {
            $loc = $m.location
            $sl  = $loc.start.line   - 1
            $sc  = $loc.start.column - 1
            $el  = $loc.end.line     - 1
            $ec  = $loc.end.column   - 1

            $original = ''
            if ($sl -ge 0 -and $sl -lt $sourceLines.Count) {
                if ($sl -eq $el) {
                    $srcLine = $sourceLines[$sl]
                    $safeEc  = if ($ec -le $srcLine.Length) { $ec } else { $srcLine.Length }
                    $safeSc  = if ($sc -le $safeEc) { $sc } else { $safeEc }
                    $original = $srcLine.Substring($safeSc, $safeEc - $safeSc)
                } else {
                    $parts = @($sourceLines[$sl].Substring($sc))
                    for ($i = $sl + 1; $i -lt $el; $i++) { $parts += $sourceLines[$i] }
                    $endLine = $sourceLines[$el]
                    $safeEc  = if ($ec -le $endLine.Length) { $ec } else { $endLine.Length }
                    $parts  += $endLine.Substring(0, $safeEc)
                    $original = $parts -join "`n"
                }
            }

            [PSCustomObject]@{
                line        = $loc.start.line
                mutator     = $m.mutatorName
                original    = $original
                replacement = $m.replacement
            }
        }

        [PSCustomObject]@{
            name           = $name
            file           = $relPath
            mutationScore  = $score
            mutantsCovered = $covered
            survived       = @($survivedList)
        }
    }

    $resultsDir = Join-Path $repoRoot 'TestResults'
    if (-not (Test-Path $resultsDir)) { New-Item -ItemType Directory -Path $resultsDir | Out-Null }

    $summary = [PSCustomObject]@{
        schemaVersion = 1
        threshold     = 60
        generatedAt   = (Get-Date).ToString('o')
        modules       = @($modules)
    }

    $summaryPath = Join-Path $resultsDir 'mutation-summary.json'
    $summary | ConvertTo-Json -Depth 10 | Set-Content $summaryPath -Encoding UTF8
    Write-Host "Mutation summary: $summaryPath"
}
