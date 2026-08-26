$ErrorActionPreference = "Stop"
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../../.."))

Write-Host "`n🔍 Running dotnet format check..." -ForegroundColor Cyan

$stagedFiles = @(
    git -C $projectRoot diff --relative --cached --name-only --diff-filter=ACMR -- . |
        Where-Object { $_ -match '\.cs$' }
)

if ($stagedFiles.Count -eq 0) {
    Write-Host "No C# files to format." -ForegroundColor Gray
    exit 0
}

Write-Host "Found $($stagedFiles.Count) C# file(s) to check." -ForegroundColor Yellow

$unstagedFiles = @(
    git -C $projectRoot diff --relative --name-only -- . |
        Where-Object { $_ -match '\.cs$' }
)
$partiallyStagedFiles = @($stagedFiles | Where-Object { $unstagedFiles -contains $_ })

if ($partiallyStagedFiles.Count -gt 0) {
    Write-Host "`nPartially staged C# files cannot be checked safely:" -ForegroundColor Red
    $partiallyStagedFiles | ForEach-Object { Write-Host "  $_" -ForegroundColor Yellow }
    Write-Host "Stage each file completely or stash its unstaged changes, then commit again." -ForegroundColor Yellow
    exit 1
}

Write-Host "Running dotnet format for: $($stagedFiles -join ', ')" -ForegroundColor Gray

Push-Location -LiteralPath $projectRoot
try {
    $formatArguments = @(
        "format",
        ".\BurcinCo.BurcinApp.slnx",
        "--include"
    ) + $stagedFiles + @(
        "--verify-no-changes",
        "--verbosity",
        "diagnostic"
    )

    & dotnet @formatArguments
    $formatExitCode = $LASTEXITCODE
}
finally {
    Pop-Location
}

if ($formatExitCode -ne 0) {
    Write-Host "`n❌ Code formatting issues detected!`n" -ForegroundColor Red
    Write-Host "Please run the following command to fix:" -ForegroundColor Yellow
    Write-Host "  dotnet format" -ForegroundColor White
    Write-Host "`nThen stage your changes and commit again." -ForegroundColor Yellow
    exit 1

    # Write-Host "Adding formatted files back to stage..." -ForegroundColor Gray
    # git add $stagedFiles;
}

Write-Host "`n✅ dotnet format check passed!" -ForegroundColor Green
exit 0
