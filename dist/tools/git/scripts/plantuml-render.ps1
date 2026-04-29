$ErrorActionPreference = "Stop"

Write-Host "`nRendering PlantUML diagrams..." -ForegroundColor Cyan

$stagedPuml = @(git diff --cached --name-only --diff-filter=ACM | Where-Object { $_ -match '\.(puml|plantuml)$' })
$deletedPuml = @(git diff --cached --name-only --diff-filter=D | Where-Object { $_ -match '\.(puml|plantuml)$' })

if ($stagedPuml.Count -eq 0 -and $deletedPuml.Count -eq 0) {
    Write-Host "No .puml changes. Skipping render." -ForegroundColor Gray
    exit 0
}

$repoRoot = git rev-parse --show-toplevel

# Verify Docker is available
$dockerCheck = docker info 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "Docker is not running or not installed. Cannot render PlantUML." -ForegroundColor Red
    exit 1
}

# Delete orphaned SVGs for removed .puml/.plantuml files
foreach ($deleted in $deletedPuml) {
    $svgRel = $deleted -replace '\.(puml|plantuml)$', '.svg'
    $svgPath = Join-Path $repoRoot $svgRel
    if (Test-Path $svgPath) {
        Write-Host "  Removing orphaned SVG: $svgRel" -ForegroundColor Yellow
        Remove-Item $svgPath -Force
        git rm --cached --quiet $svgRel 2>$null
    }
}

if ($stagedPuml.Count -gt 0) {
    Write-Host "Rendering $($stagedPuml.Count) PlantUML file(s) via Docker..." -ForegroundColor Yellow

    # Convert Windows paths to forward-slash for Docker
    $mountPath = ($repoRoot -replace '\\', '/')

    # Render in-place: -o '.' tells PlantUML to output next to source file
    # -nometadata strips version/timestamp for stable diffs
    # -failfast2 fails immediately on any error
    $pumlArgs = @('run', '--rm', '-v', "${mountPath}:/data", '-w', '/data',
                  'plantuml/plantuml', '-tsvg', '-nometadata', '-failfast2', '-o', '.') + $stagedPuml

    & docker @pumlArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Host "`nPlantUML render failed!" -ForegroundColor Red
        exit 1
    }

    # Stage generated SVGs
    foreach ($puml in $stagedPuml) {
        $svgRel = $puml -replace '\.(puml|plantuml)$', '.svg'
        $svgAbs = Join-Path $repoRoot $svgRel
        if (Test-Path $svgAbs) {
            git add $svgAbs
            Write-Host "  Staged: $svgRel" -ForegroundColor Green
        } else {
            Write-Host "  WARNING: Expected SVG not found: $svgRel" -ForegroundColor Yellow
        }
    }
}

Write-Host "`nPlantUML render complete!" -ForegroundColor Green
exit 0
