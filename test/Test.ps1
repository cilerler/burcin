$repository = "issue.cilerler_bedia_1";
$database = "WideWorldImporters";
$datetime = $(get-date -Format "yyyyMMdd");
$folderPath = "$env:userprofile\Source\local\$datetime\$repository";
$originalLocation = Get-Location;
nuget pack burcin.nuspec;
dotnet new -i Burcin.Templates.CSharp.0.0.1.nupkg;
Remove-Item .\Burcin.Templates.CSharp.0.0.1.nupkg;
Remove-Item -Recurse -Force $folderPath;
New-Item -ItemType "directory" -Path $folderPath -ErrorAction Ignore;
Set-Location $folderPath
dotnet new burcin --WebApiApplication --HealthChecks --Swagger --BlazorApplication --ConsoleApplication --WindowsService --BackgroundService --EntityFramework --DatabaseName $database --TestFramework --DocFx --DockerSupport --NugetSourceGitHub --NugetSourceAzureDevOps --VsCodeDirectory --GitHubTemplates --Cache "All" --Authors "Cengiz Ilerler" --RepositoryUrl "https://github.com/cilerler/$repository" --SkipRestore;
dotnet new -u Burcin.Templates.CSharp.0.0.1;
&".\tools\createSolutionFile.ps1";
code .;
Set-Location $originalLocation;
