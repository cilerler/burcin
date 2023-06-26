# Ensure that you navigate to the project's root folder before proceeding!
# Set-Location "$env:userprofile\Source\burcin";
# PowerShell.exe -ExecutionPolicy Bypass -File "$env:userprofile\Source\burcin\test\Test.ps1";
$repository = "issue.cilerler_wwi_"+$(get-date -Format "HHmmss");
$database = "WideWorldImporters";
$datetime = $(get-date -Format "yyyyMMdd");
$folderPath = "$env:userprofile\Source\local\$datetime\$repository";
$originalLocation = Get-Location;
nuget pack burcin.nuspec;
dotnet new install .\Burcin.Templates.CSharp.0.0.1.nupkg;
Remove-Item .\Burcin.Templates.CSharp.0.0.1.nupkg;
Remove-Item -Recurse -Force $folderPath;
New-Item -ItemType "directory" -Path $folderPath -ErrorAction Ignore;
Set-Location $folderPath
dotnet new burcin --WebApiApplication --HealthChecks --Swagger --BlazorApplication --ConsoleApplication --OData --WindowsService --BackgroundService --EntityFramework --DatabaseName $database --TestFramework --DocFx --DockerSupport --SerilogSupport --NugetSourceGitHub --NugetSourceAzureDevOps --VsCodeDirectory --GitHubTemplates --Cache "All" --Authors "Cengiz Ilerler" --RepositoryUrl "https://github.com/cilerler/$repository" --SkipRestore;
# dotnet new burcin --WebApiApplication --HealthChecks --Swagger --ConsoleApplication --OData --WindowsService --BackgroundService --EntityFramework --DatabaseName $database --TestFramework --DocFx --DockerSupport --SerilogSupport --NugetSourceGitHub --NugetSourceAzureDevOps --VsCodeDirectory --GitHubTemplates --Cache "All" --Authors "Cengiz Ilerler" --RepositoryUrl "https://github.com/cilerler/$repository" --SkipRestore;
# dotnet new burcin --WebApiApplication --HealthChecks --Swagger --ConsoleApplication --OData --WindowsService --BackgroundService --EntityFramework --DatabaseName $database --TestFramework --DocFx --DockerSupport --SerilogSupport --NugetSourceGitHub --VsCodeDirectory --GitHubTemplates --Cache "All" --Authors "Cengiz Ilerler" --RepositoryUrl "https://github.com/cilerler/$repository" --SkipRestore;
# dotnet new burcin --ConsoleApplication --WindowsService --BackgroundService --EntityFramework --DatabaseName $database --TestFramework --DocFx --DockerSupport --SerilogSupport --VsCodeDirectory --GitHubTemplates --Cache "All" --Authors "Cengiz Ilerler" --RepositoryUrl "https://github.com/cilerler/$repository" --SkipRestore;
# dotnet new burcin --BlazorApplication --OData --EntityFramework --SerilogSupport --Cache "None" --Authors "Cengiz Ilerler" --RepositoryUrl "https://github.com/cilerler/$repository" --SkipRestore;
# dotnet new burcin --BlazorApplication --EntityFramework --SerilogSupport --Cache "None" --Authors "Cengiz Ilerler" --RepositoryUrl "https://github.com/cilerler/$repository" --SkipRestore;
# dotnet new burcin --BlazorApplication --SerilogSupport --Cache "None" --Authors "Cengiz Ilerler" --RepositoryUrl "https://github.com/cilerler/$repository" --SkipRestore;
# dotnet new burcin --HealthChecks --BlazorApplication --SerilogSupport --VsCodeDirectory --Cache "None" --Authors "Cengiz Ilerler" --RepositoryUrl "https://github.com/cilerler/$repository" --SkipRestore;
# dotnet new burcin --BlazorApplication --OData --Cache "None" --Authors "Cengiz Ilerler" --RepositoryUrl "https://github.com/cilerler/$repository" --SkipRestore;
dotnet new uninstall Burcin.Templates.CSharp;
&".\tools\createSolutionFile.ps1";
code .;
Set-Location $originalLocation;
