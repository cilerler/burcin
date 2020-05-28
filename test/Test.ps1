$repository = "Bedia";
nuget pack burcin.nuspec;
dotnet new -i Burcin.Templates.CSharp.0.0.1.nupkg;
Remove-Item .\Burcin.Templates.CSharp.0.0.1.nupkg;
Remove-Item -Recurse -Force $repository;
New-Item -ItemType "directory" -Name $repository;
Set-Location $repository;
dotnet new burcin --WebApiApplication --HealthChecks --Swagger --BlazorApplication --ConsoleApplication --WindowsService --BackgroundService --EntityFramework --DatabaseName $repository --TestFramework --DocFx --DockerSupport --NugetSourceGitHub --NugetSourceAzureDevOps --VsCodeDirectory --GitHubTemplates --Cache "All" --Authors "Cengiz Ilerler" --RepositoryUrl "https://github.com/cilerler/bedia" --SkipRestore;
dotnet new -u Burcin.Templates.CSharp.0.0.1;
&".\tools\createSolutionFile.ps1";
code .;
Set-Location ..;
