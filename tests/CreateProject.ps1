param(
	[switch]$Versioned,
	# Drops the target database on the persistent local mssql instance before regen so the
	# subsequent `dotnet ef database update` runs against a fresh schema. Off by default —
	# day-to-day regens shouldn't nuke the dev DB unless explicitly asked.
	[switch]$DropDatabase
)

Push-Location $PSScriptRoot
try {
	# Load secrets/personal values from .env (gitignored). See .env.example for the schema.
	$envFile = Join-Path $PSScriptRoot ".env"
	if (-not (Test-Path $envFile)) {
		throw "Missing tests/.env. Copy tests/.env.example to tests/.env and fill in your values."
	}
	$envValues = @{}
	foreach ($line in Get-Content $envFile) {
		if ($line -match '^\s*#' -or $line -match '^\s*$') { continue }
		if ($line -match '^\s*([A-Z_][A-Z0-9_]*)\s*=\s*(.*?)\s*$') {
			$value = $matches[2]
			if ($value -match '^"(.*)"$' -or $value -match "^'(.*)'$") { $value = $matches[1] }
			$envValues[$matches[1]] = $value
		}
	}
	$required = @('ORGANIZATION_LEGAL_NAME', 'ORGANIZATION_NAME', 'REPOSITORY_NAME', 'PROJECT_NAME', 'AUTHORS', 'DATABASE_NAME')
	$missing = $required | Where-Object { -not $envValues.ContainsKey($_) -or [string]::IsNullOrWhiteSpace($envValues[$_]) }
	if ($missing) {
		throw "Missing or empty required key(s) in tests/.env"
	}

	Set-Location ".\..";
	$organizationLegalName = $envValues['ORGANIZATION_LEGAL_NAME']
	$organizationName = $envValues['ORGANIZATION_NAME']
	$repositoryName = $envValues['REPOSITORY_NAME']
	$projectName = $envValues['PROJECT_NAME']
	$authors = $envValues['AUTHORS']
	$databaseName = $envValues['DATABASE_NAME']
	$folderPath = ".\tests\TestResults.ignore";
	if ($Versioned) {
		$datetime = $(get-date -Format "yyyyMMddHHmmss");
		$folderPath = "$folderPath\$datetime";
	}

	if ($DropDatabase) {
		# Drop the database on the persistent local mssql container (Aspire-managed instances are
		# ephemeral per test-run; the persistent one is what dev/EF tooling targets).
		# Run sqlcmd inside the container so we don't need it on the host PATH. Force single-user to
		# evict any open connections (Aspire Host, EF tooling, your editor's MSSQL extension, etc.)
		# before drop.
		Write-Host "Dropping database '$databaseName' on the persistent mssql container..." -ForegroundColor Yellow;
		$dropQuery = "USE master; IF DB_ID('$databaseName') IS NOT NULL BEGIN ALTER DATABASE [$databaseName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$databaseName]; PRINT 'Dropped $databaseName.'; END ELSE PRINT 'Database $databaseName did not exist; nothing to drop.';";
		docker exec mssql /opt/mssql-tools18/bin/sqlcmd -S "127.0.0.1,1433" -U sa -P "PasswordAdmin1!" -C -Q $dropQuery;
		if ($LASTEXITCODE -ne 0) {
			throw "Drop failed with exit code $LASTEXITCODE. Is the persistent 'mssql' container running?";
		}
	}

	nuget pack burcin.nuspec -NoDefaultExcludes;
	$nupkg = Get-ChildItem -Filter "Burcin.Templates.CSharp.*.nupkg" | Select-Object -First 1;
	dotnet new install $nupkg.FullName; # .\dist;

	Remove-Item $nupkg.FullName;
	Remove-Item -Recurse -Force $folderPath;
	New-Item -ItemType "directory" -Path $folderPath -ErrorAction Ignore;
	Set-Location $folderPath

	dotnet new burcin --name $repositoryName `
	--OrganizationLegalName $organizationLegalName --OrganizationName $organizationName --ProjectName $projectName `
	--EntityFramework --OData `
	--Sample `
	--DatabaseName $databaseName `
	--DocFx --NugetSourceGitHub --NugetSourceAzureDevOps --GitHubTemplates `
	--Cache "All" --Authors $authors `
	--RepositoryUrl "https://github.com/$organizationName/$repositoryName" `
	--SkipRestore;

	Remove-Item -Recurse -Force ".\$repositoryName\nuget.config";
}
finally {
	dotnet new uninstall Burcin.Templates.CSharp; # .\dist;
	# dotnet new list burcin;
	Pop-Location
}
