param(
	[switch]$Versioned,
	# Drops the target database on the persistent local mssql instance before regen so the
	# subsequent `dotnet ef database update` runs against a fresh schema. Off by default —
	# day-to-day regens shouldn't nuke the dev DB unless explicitly asked.
	[switch]$DropDatabase
)

$ErrorActionPreference = "Stop"
$templateHive = $null
$templateInstalled = $false
$nupkg = $null

Push-Location $PSScriptRoot
try {
	# Load secrets/personal values from .env (gitignored). See .env.example for the schema.
	$envFile = Join-Path $PSScriptRoot ".env"
	if (-not (Test-Path -LiteralPath $envFile -PathType Leaf)) {
		throw "Missing tests/.env. Copy tests/.env.example to tests/.env and fill in your values."
	}

	$envValues = @{}
	foreach ($line in Get-Content -LiteralPath $envFile) {
		if ($line -match '^\s*#' -or $line -match '^\s*$') { continue }
		if ($line -match '^\s*([A-Z_][A-Z0-9_]*)\s*=\s*(.*?)\s*$') {
			$key = $matches[1]
			$value = $matches[2]
			if ($value -match '^"(.*)"$' -or $value -match "^'(.*)'$") { $value = $matches[1] }
			$envValues[$key] = $value
		}
	}

	$required = @('ORGANIZATION_LEGAL_NAME', 'ORGANIZATION_NAME', 'REPOSITORY_NAME', 'PROJECT_NAME', 'AUTHORS', 'DATABASE_NAME')
	$missing = $required | Where-Object { -not $envValues.ContainsKey($_) -or [string]::IsNullOrWhiteSpace($envValues[$_]) }
	if ($missing) {
		throw "Missing or empty required key(s) in tests/.env: $($missing -join ', ')"
	}

	Set-Location (Join-Path $PSScriptRoot "..")
	$organizationLegalName = $envValues['ORGANIZATION_LEGAL_NAME']
	$organizationName = $envValues['ORGANIZATION_NAME']
	$repositoryName = $envValues['REPOSITORY_NAME']
	$projectName = $envValues['PROJECT_NAME']
	$authors = $envValues['AUTHORS']
	$databaseName = $envValues['DATABASE_NAME']

	if ($repositoryName -in @('.', '..') -or
		$repositoryName -ne $repositoryName.Trim() -or
		$repositoryName.IndexOfAny([System.IO.Path]::GetInvalidFileNameChars()) -ge 0 -or
		[System.IO.Path]::GetFileName($repositoryName) -ne $repositoryName) {
		throw "REPOSITORY_NAME must be a valid single directory name without leading/trailing whitespace or path separators; received '$repositoryName'."
	}

	$testResultsRootPath = [System.IO.Path]::GetFullPath(".\tests\TestResults.ignore")
	New-Item -ItemType Directory -Path $testResultsRootPath -Force | Out-Null
	$templateHive = Join-Path $testResultsRootPath (".template-hive-{0}" -f [guid]::NewGuid().ToString("N"))
	$fixtureContainerPath = $testResultsRootPath
	if ($Versioned) {
		$fixtureContainerPath = Join-Path $testResultsRootPath (Get-Date -Format "yyyyMMddHHmmss")
	}

	$generatedProjectPath = [System.IO.Path]::GetFullPath((Join-Path $fixtureContainerPath $repositoryName))
	$expectedPrefix = $testResultsRootPath.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
	if (-not $generatedProjectPath.StartsWith($expectedPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
		throw "Refusing to generate or clean outside '$testResultsRootPath': '$generatedProjectPath'."
	}

	if ($DropDatabase) {
		if ($databaseName -notmatch '^[A-Za-z_][A-Za-z0-9_]{0,127}$') {
			throw "DATABASE_NAME must be a regular SQL identifier when -DropDatabase is selected; received '$databaseName'."
		}

		# Drop the database on the persistent local mssql container (Aspire-managed instances are
		# ephemeral per test-run; the persistent one is what dev/EF tooling targets).
		Write-Host "Dropping database '$databaseName' on the persistent mssql container..." -ForegroundColor Yellow
		$dropQuery = "USE master; IF DB_ID('$databaseName') IS NOT NULL BEGIN ALTER DATABASE [$databaseName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$databaseName]; PRINT 'Dropped $databaseName.'; END ELSE PRINT 'Database $databaseName did not exist; nothing to drop.';"
		docker exec mssql /opt/mssql-tools18/bin/sqlcmd -S "127.0.0.1,1433" -U sa -P "PasswordAdmin1!" -C -Q $dropQuery
		if ($LASTEXITCODE -ne 0) {
			throw "Drop failed with exit code $LASTEXITCODE. Is the persistent 'mssql' container running?"
		}
	}

	# Pack through the same project used by CI so dotfiles survive and no nuget.exe dependency is required.
	$packageOutputPath = ".\artifacts\packages"
	dotnet pack .\burcin.pack.csproj --output $packageOutputPath -p:NuspecProperties="version=10.0.0-local"
	if ($LASTEXITCODE -ne 0) { throw "dotnet pack failed with exit code $LASTEXITCODE." }

	$nupkg = Get-ChildItem -Path $packageOutputPath -Filter "Burcin.Templates.CSharp.10.0.0-local.nupkg" | Select-Object -First 1
	if ($null -eq $nupkg) { throw "Packed template was not found under '$packageOutputPath'." }

	# Use a private hive so verification never replaces or uninstalls the developer's global template.
	dotnet new --debug:custom-hive $templateHive install $nupkg.FullName
	if ($LASTEXITCODE -ne 0) { throw "Template install failed with exit code $LASTEXITCODE." }
	$templateInstalled = $true

	if (Test-Path -LiteralPath $generatedProjectPath) {
		Remove-Item -LiteralPath $generatedProjectPath -Recurse -Force
	}
	New-Item -ItemType Directory -Path $fixtureContainerPath -Force | Out-Null
	Set-Location $fixtureContainerPath

	dotnet new --debug:custom-hive $templateHive burcin --name $repositoryName `
		--OrganizationLegalName $organizationLegalName --OrganizationName $organizationName --ProjectName $projectName `
		--EntityFramework --OData --Sample `
		--DatabaseName $databaseName `
		--DocFx --NugetSourceGitHub --NugetSourceAzureDevOps --GitHubTemplates `
		--Cache "All" --Authors $authors `
		--RepositoryUrl "https://github.com/$organizationName/$repositoryName" `
		--SkipRestore
	if ($LASTEXITCODE -ne 0) { throw "Template generation failed with exit code $LASTEXITCODE." }

	Write-Host "Generated fixture: $generatedProjectPath" -ForegroundColor Green
}
finally {
	if ($templateInstalled -and $templateHive) {
		try {
			dotnet new --debug:custom-hive $templateHive uninstall Burcin.Templates.CSharp | Out-Null
			if ($LASTEXITCODE -ne 0) { Write-Warning "Private template uninstall failed with exit code $LASTEXITCODE." }
		}
		catch {
			Write-Warning "Private template uninstall failed: $($_.Exception.Message)"
		}
	}
	if ($nupkg -and (Test-Path -LiteralPath $nupkg.FullName)) {
		try { Remove-Item -LiteralPath $nupkg.FullName -Force }
		catch { Write-Warning "Could not remove temporary template package '$($nupkg.FullName)': $($_.Exception.Message)" }
	}
	if ($templateHive -and (Test-Path -LiteralPath $templateHive)) {
		try { Remove-Item -LiteralPath $templateHive -Recurse -Force }
		catch { Write-Warning "Could not remove private template hive '$templateHive': $($_.Exception.Message)" }
	}
	Pop-Location
}
