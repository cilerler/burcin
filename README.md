# Burcin 

[![Open in Visual Studio Code](https://open.vscode.dev/badges/open-in-vscode.svg)](https://open.vscode.dev/cilerler/burcin)
[![](https://img.shields.io/badge/stackoverflow-burcin-orange.svg?style=for-the-badge&logo=stackoverflow)](https://stackoverflow.com/questions/tagged/burcin)
![](https://img.shields.io/github/release/cilerler/burcin.svg?style=for-the-badge&logo=github)
![](https://img.shields.io/github/downloads/cilerler/burcin/latest/total.svg?style=for-the-badge&logo=github&color=yellow)
 
[![](https://img.shields.io/nuget/v/Burcin.Templates.CSharp.svg?logo=nuget)](https://www.nuget.org/packages/Burcin.Templates.CSharp)
![](https://img.shields.io/nuget/dt/Burcin.Templates.CSharp.svg?logo=nuget&color=yellow)
![ci](https://github.com/cilerler/burcin/workflows/ci/badge.svg?branch=main)


The template will change all `Burcin` words under the `dist` folder to the folder name.

## Install

```pwsh
# retrieves latest
dotnet new install "Burcin.Templates.CSharp"

# retrieves a specific version with source definition
dotnet new install "Burcin.Templates.CSharp::1.2.21" --nuget-source https://api.nuget.org/v3/index.json
```

## Update

> [!WARNING]
> It looks like `--update-*` commands are not working (4/22/2020)

```pwsh
# checks if tere is an update
dotnet new "Burcin.Templates.CSharp" --update-check
```

```pwsh
# applies if tere is an update
dotnet new "Burcin.Templates.CSharp" --update-apply
```

## Uninstall

```pwsh
dotnet new uninstall "Burcin.Templates.CSharp"
```

## Help

```pwsh
dotnet new burcin --help
```

## Run

```pwsh
cd "<PATH>"; #e.g. C:\Users\<USERNAME>\Source\local\<MYPROJECT>

# Full Modular Polylith on .NET 10 (the canonical example):
dotnet new burcin --name "MyProject" `
    --OrganizationLegalName "Acme, Inc." --OrganizationName "Acme" --ProjectName "MyApp" `
    --DatabaseName "MyAppDb" --Authors "Your Name" `
    --RepositoryUrl "https://github.com/<changeme>/myproject" `
    --EntityFramework --OData --Cache "All" `
    --DocFx --GitHubTemplates --NugetSourceGitHub --NugetSourceAzureDevOps `
    --SkipRestore;

# Minimal scaffold (no EF / no modules / no OData) — useful for stateless services:
dotnet new burcin --name "MyService" `
    --OrganizationLegalName "Acme, Inc." --OrganizationName "Acme" --ProjectName "MyService" `
    --DatabaseName "MyServiceDb" --Authors "Your Name" `
    --SkipRestore;
```

See the generated project's `README.md` for architecture details — the Modular Polylith pattern,
per-module schemas, Outbox/Inbox flows, Aspire AppHost orchestration, and how to add a new module.

## List all templates

```pwsh
dotnet new list burcin;
```
