$ErrorActionPreference = "Stop"

# comment the following line to enable pre-commit checks
exit 0;

Write-Host "`n🔍 Running pre-commit checks..." -ForegroundColor Cyan

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

& "$scriptDir/scripts/plantuml-render.ps1"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& "$scriptDir/scripts/dotnet-format.ps1"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }


Write-Host "`n✅ All checks passed!" -ForegroundColor Green
exit 0
