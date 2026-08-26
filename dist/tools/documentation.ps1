[CmdletBinding()]
param(
	[Parameter()]
	[switch]$Serve
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$docfxConfigRelativePath = "docs/docfx/docfx.json"
$docfxConfigPath = Join-Path $repositoryRoot $docfxConfigRelativePath
$docfxConfigDirectory = Split-Path -Parent $docfxConfigPath

Push-Location -LiteralPath $repositoryRoot
try {
	& dotnet tool restore
	if ($LASTEXITCODE -ne 0) {
		throw "dotnet tool restore failed with exit code $LASTEXITCODE."
	}

	# Restore only the projects selected for managed API metadata. Reading that selection from
	# docfx.json keeps MAUI and other deployable projects out of the documentation restore graph.
	$docfxConfig = Get-Content -Raw -LiteralPath $docfxConfigPath | ConvertFrom-Json
	$metadataProperty = $docfxConfig.PSObject.Properties["metadata"]
	$apiProjects = if ($null -eq $metadataProperty) {
		@()
	}
	else {
		@(
			foreach ($metadataEntry in @($metadataProperty.Value)) {
				foreach ($sourceEntry in @($metadataEntry.src)) {
					$sourceRoot = [System.IO.Path]::GetFullPath((Join-Path $docfxConfigDirectory $sourceEntry.src))
					foreach ($filePattern in @($sourceEntry.files)) {
						$projectFilePattern = Split-Path -Leaf ($filePattern -replace '/', [System.IO.Path]::DirectorySeparatorChar)
						Get-ChildItem -LiteralPath $sourceRoot -Filter $projectFilePattern -File -Recurse
					}
				}
			}
		) | Sort-Object -Property FullName -Unique
	}

	foreach ($apiProject in @($apiProjects)) {
		& dotnet restore $apiProject.FullName
		if ($LASTEXITCODE -ne 0) {
			throw "dotnet restore '$($apiProject.FullName)' failed with exit code $LASTEXITCODE."
		}
	}

	$docfxArguments = @(
		"docfx",
		$docfxConfigRelativePath,
		"--warningsAsErrors",
		"true"
	)

	if ($Serve) {
		$docfxArguments += "--serve"
	}

	& dotnet @docfxArguments
	if ($LASTEXITCODE -ne 0) {
		throw "dotnet $($docfxArguments -join ' ') failed with exit code $LASTEXITCODE."
	}
}
finally {
	Pop-Location
}
