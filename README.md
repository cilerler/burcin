# Burcin 

[![Open in Visual Studio Code](https://open.vscode.dev/badges/open-in-vscode.svg)](https://open.vscode.dev/cilerler/burcin)
[![](https://img.shields.io/badge/stackoverflow-burcin-orange.svg?style=for-the-badge&logo=stackoverflow)](https://stackoverflow.com/questions/tagged/burcin)
![](https://img.shields.io/github/release/cilerler/burcin.svg?style=for-the-badge&logo=github)
![](https://img.shields.io/github/downloads/cilerler/burcin/latest/total.svg?style=for-the-badge&logo=github&color=yellow)
 
[![](https://img.shields.io/nuget/v/Burcin.Templates.CSharp.svg?logo=nuget)](https://www.nuget.org/packages/Burcin.Templates.CSharp)
![](https://img.shields.io/nuget/dt/Burcin.Templates.CSharp.svg?logo=nuget&color=yellow)
![ci](https://github.com/cilerler/burcin/workflows/ci/badge.svg?branch=main)


The template replaces the `BurcinCo`, `BurcinApp`, and `BurcinDatabase` sentinel names with the
organization, project, and database values supplied at generation time. The Burcin template name
and attribution remain Burcin.

## Install

```pwsh
# retrieves latest
dotnet new install "Burcin.Templates.CSharp"

# retrieves a specific version with source definition
dotnet new install "Burcin.Templates.CSharp@10.0.26" --nuget-source https://api.nuget.org/v3/index.json
```

## Update

> [!WARNING]
> It looks like `--update-*` commands are not working (4/22/2020)

```pwsh
# checks if there is an update
dotnet new "Burcin.Templates.CSharp" --update-check
```

```pwsh
# applies if there is an update
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
dotnet new burcin --name "MyFolder" `
    --OrganizationLegalName "MyOrganization, Inc." --OrganizationName "MyOrganization" --ProjectName "MyProject" `
    --DatabaseName "MyProjectDb" --Authors "Your Name" `
    --RepositoryUrl "https://github.com/<changeme>/myproject" `
    --Sample --Cache "All" `
    --Web --Maui `
    --DocFx --GitHubTemplates `
    --SkipRestore;

# Minimal scaffold (no EF / no reference modules / no OData):
dotnet new burcin --name "MyFolder" `
    --OrganizationLegalName "MyOrganization, Inc." --OrganizationName "MyOrganization" --ProjectName "MyService" `
    --DatabaseName "MyServiceDb" --Authors "Your Name" `
    --SkipRestore;
```

The client switches are independent. Either one generates the shared Razor Class Library; neither is enabled
for the minimal scaffold unless selected.

| Selection | Generated client projects |
|---|---|
| neither | none |
| `--Web` | `Client.Shared`, `Client.Web` |
| `--Maui` | `Client.Shared`, `Client.Maui` |
| `--Web --Maui` | `Client.Shared`, `Client.Web`, `Client.Maui` |

Reusable Razor UI lives once in `Client.Shared`. Web owns its Blazor server shell and Dockerfile, and the
Gateway exposes that runner at `/portal`. MAUI owns its native shell and the app-local `wwwroot/index.html`
required by `BlazorWebView`; it maps the shared `Routes` component directly and does not duplicate the
shared UI.

Every generated repository includes a modern .NET 10 + Aspire Dev Container with Docker-in-Docker. See the
[generated-project setup instructions](dist/README.md#prerequisites) for its host requirements, persistent
development state, and native-MAUI boundary.

DocFX is opt-in. Add `--DocFx` to generate a searchable site containing the canonical Markdown documentation,
ADRs, and curated managed .NET API reference. The API reference is part of DocFX rather than a separate template
option. Authored Markdown under `docs/` is generated with or without the site tooling.

The generated project's `README.md` reflects the selected options. With `--Sample`, it documents
the Modular Polylith reference modules, per-module schemas, Outbox/Inbox flows, Aspire AppHost
orchestration, the Gateway-owned Webhook edge adapter, and how to add a new module; minimal
output omits claims about projects it did not generate.

## Verify local template changes

```pwsh
Copy-Item tests/.env.example tests/.env
# Fill in tests/.env, then generate the canonical Sample fixture with a private template hive.
./tests/CreateProject.ps1 -Versioned
```

`CreateProject.ps1` packs the current template, installs it into an isolated private hive, and writes the
generated fixture with both client runners beneath `tests/TestResults.ignore` without changing the globally
installed template.

## List all templates

```pwsh
dotnet new list burcin;
```
