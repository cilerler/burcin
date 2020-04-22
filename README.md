# Burcin 

[![](https://img.shields.io/badge/stackoverflow-burcin-orange.svg?style=for-the-badge&logo=stackoverflow)](https://stackoverflow.com/questions/tagged/burcin)
![](https://img.shields.io/github/release/cilerler/burcin.svg?style=for-the-badge&logo=github)
![](https://img.shields.io/github/downloads/cilerler/burcin/latest/total.svg?style=for-the-badge&logo=github&color=yellow)
 
[![](https://img.shields.io/nuget/v/Burcin.Templates.CSharp.svg?logo=nuget)](https://www.nuget.org/packages/Burcin.Templates.CSharp)
![](https://img.shields.io/nuget/dt/Burcin.Templates.CSharp.svg?logo=nuget&color=yellow)
![](https://github.com/cilerler/burcin/workflows/build_on_push/badge.svg)


The template will change all `Burcin` words under the `dist` folder to the folder name.

## Install

```powershell
# retrieves latest
dotnet new --install "Burcin.Templates.CSharp"

# retrieves a specific version with source definition
dotnet new --install "Burcin.Templates.CSharp::1.2.21" --nuget-source https://api.nuget.org/v3/index.json
```

## Update

> [!WARNING]
> It looks like `--update-*` commands are not working (4/22/2020)

```powershell
# checks if tere is an update
dotnet new "Burcin.Templates.CSharp" --update-check
```

```powershell
# applies if tere is an update
dotnet new "Burcin.Templates.CSharp" --update-apply
```

## Uninstall

```powershell
dotnet new --uninstall "Burcin.Templates.CSharp"
```

## Help

```powershell
dotnet new burcin --help
```

## Run

```powershell
cd "<PATH>"; #e.g. C:\Users\<USERNAME>\Source\local\<MYPROJECT>

dotnet new burcin --WebApiApplication --HealthChecks --Swagger --ConsoleApplication --WindowsService --BackgroundService --EntityFramework --DatabaseName "ChangeMe" --TestFramework --DocFx --DockerSupport --NugetSourceGitHub --NugetSourceAzureDevOps --VsCodeDirectory --GitHubTemplates --Cache "All" --Authors "Cengiz Ilerler" --RepositoryUrl "https://github.com/cilerler/bedia" --SkipRestore;
```
