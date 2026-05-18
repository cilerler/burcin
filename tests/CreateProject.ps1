param(
	[switch]$Versioned,
	# Drops the target database on the persistent local mssql instance before regen so the
	# subsequent `dotnet ef database update` runs against a fresh schema. Off by default —
	# day-to-day regens shouldn't nuke the dev DB unless explicitly asked.
	[switch]$DropDatabase
)

Push-Location $PSScriptRoot
try {
	Set-Location ".\..";
	$organizationLegalName = "OneDeveloperWay, Inc.";
	$organizationName = "OneDeveloperWay";
	$repositoryName = "zignec"
	$projectName = "Zignec";
	$authors = "Cengiz Ilerler";
	$databaseName = "Zignec";
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

	Remove-Item -Recurse -Force ".\zignec\nuget.config";
}
finally {
	dotnet new uninstall Burcin.Templates.CSharp; # .\dist;
	# dotnet new list burcin;
	Pop-Location
}
