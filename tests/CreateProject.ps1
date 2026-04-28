param(
	[switch]$Versioned
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
