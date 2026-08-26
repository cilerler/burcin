$ErrorActionPreference = "Stop"
$plantUmlImage = "plantuml/plantuml@sha256:f2c8916a795483bf32ea61ca63b1c6726845c0085c997d86431e20b52ca1c257"
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../../.."))

Write-Host "`nRendering PlantUML diagrams..." -ForegroundColor Cyan

$stagedPuml = @(
    git -C $projectRoot diff --relative --cached --name-only --no-renames --diff-filter=ACM -- . |
        Where-Object { $_ -match '\.(puml|plantuml)$' }
)
$deletedPuml = @(
    git -C $projectRoot diff --relative --cached --name-only --no-renames --diff-filter=D -- . |
        Where-Object { $_ -match '\.(puml|plantuml)$' }
)

if ($stagedPuml.Count -eq 0 -and $deletedPuml.Count -eq 0) {
    Write-Host "No .puml changes. Skipping render." -ForegroundColor Gray
    exit 0
}

$unstagedPuml = @(
    git -C $projectRoot diff --relative --name-only -- . |
        Where-Object { $_ -match '\.(puml|plantuml)$' }
)
$changedPuml = @($stagedPuml) + @($deletedPuml)
$partiallyStagedPuml = @($changedPuml | Where-Object { $unstagedPuml -contains $_ })

if ($partiallyStagedPuml.Count -gt 0) {
    Write-Host "`nPartially staged PlantUML files cannot be rendered safely:" -ForegroundColor Red
    $partiallyStagedPuml | ForEach-Object { Write-Host "  $_" -ForegroundColor Yellow }
    Write-Host "Stage each file completely or stash its unstaged changes, then commit again." -ForegroundColor Yellow
    exit 1
}

# Delete orphaned SVGs for removed .puml/.plantuml files
foreach ($deleted in $deletedPuml) {
    $svgRel = $deleted -replace '\.(puml|plantuml)$', '.svg'
    $svgPath = Join-Path $projectRoot $svgRel
    if (Test-Path $svgPath) {
        Write-Host "  Removing orphaned SVG: $svgRel" -ForegroundColor Yellow
        Remove-Item $svgPath -Force
    }

    git -C $projectRoot rm --cached --ignore-unmatch --quiet --force -- $svgRel
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  ERROR: Could not stage SVG deletion: $svgRel" -ForegroundColor Red
        exit 1
    }
}

if ($stagedPuml.Count -gt 0) {
    # Docker is needed only when a source file must be rendered. Deletion-only commits work without it.
    $dockerCheck = docker info 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Docker is not running or not installed. Cannot render PlantUML." -ForegroundColor Red
        exit 1
    }

    Write-Host "Rendering $($stagedPuml.Count) PlantUML file(s) via Docker..." -ForegroundColor Yellow

    # Convert Windows paths to forward-slash for Docker
    $mountPath = ($projectRoot -replace '\\', '/')

    # Render in-place: -o '.' tells PlantUML to output next to source file
    # -nometadata strips version/timestamp for stable diffs
    # -failfast2 fails immediately on any error
    $pumlArgs = @('run', '--rm', '-v', "${mountPath}:/data", '-w', '/data',
                  $plantUmlImage, '-tsvg', '-nometadata', '-failfast2', '-o', '.') + $stagedPuml

    & docker @pumlArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Host "`nPlantUML render failed!" -ForegroundColor Red
        exit 1
    }

    # Stage generated SVGs
    foreach ($puml in $stagedPuml) {
        $svgRel = $puml -replace '\.(puml|plantuml)$', '.svg'
        $svgAbs = Join-Path $projectRoot $svgRel
        if (Test-Path $svgAbs) {
            & (Join-Path $PSScriptRoot "normalize-plantuml-template-svg.ps1") -SvgPath $svgAbs
            git -C $projectRoot add -- $svgRel
            Write-Host "  Staged: $svgRel" -ForegroundColor Green
        } else {
            Write-Host "  ERROR: Expected SVG not found: $svgRel" -ForegroundColor Red
            exit 1
        }
    }
}

Write-Host "`nPlantUML render complete!" -ForegroundColor Green
exit 0
