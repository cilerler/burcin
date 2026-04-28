$ErrorActionPreference = "Stop"

# comment the following line to enable pre-commit checks
exit 0;

Write-Host "`n🔍 Running pre-commit checks..." -ForegroundColor Cyan

$stagedFiles = @(git diff --cached --name-only --diff-filter=ACM "*.cs")

if ($stagedFiles.Count -eq 0) {
    Write-Host "No C# files to format." -ForegroundColor Gray
    exit 0
}

Write-Host "Found $($stagedFiles.Count) C# file(s) to check." -ForegroundColor Yellow

$fileList = $stagedFiles -join ','

Write-Host "Running: dotnet format --include $fileList --verify-no-changes" -ForegroundColor Gray

dotnet format ".\BurcinCo.BurcinApp.slnx" --include $fileList --verify-no-changes --verbosity diagnostic

if ($LASTEXITCODE -ne 0) {
    Write-Host "`n❌ Code formatting issues detected!`n" -ForegroundColor Red
    Write-Host "Please run the following command to fix:" -ForegroundColor Yellow
    Write-Host "  dotnet format" -ForegroundColor White
    Write-Host "`nThen stage your changes and commit again." -ForegroundColor Yellow
    exit 1

    # Write-Host "Adding formatted files back to stage..." -ForegroundColor Gray
    # git add $stagedFiles;
}

Write-Host "`n✅ All checks passed!" -ForegroundColor Green
exit 0
