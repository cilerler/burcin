param(
    [Parameter(Mandatory)]
    [string] $HooksDirectory
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $HooksDirectory -PathType Container)) {
    Write-Output "false"
    exit 0
}

$activeHooks = @(
    Get-ChildItem -LiteralPath $HooksDirectory -File -ErrorAction Stop |
        Where-Object { -not $_.Name.EndsWith(".sample", [StringComparison]::OrdinalIgnoreCase) }
)

Write-Output (($activeHooks.Count -gt 0).ToString().ToLowerInvariant())
