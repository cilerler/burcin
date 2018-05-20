```
       _ _           _
      (_) |         | |
   ___ _| | ___ _ __| | ___ _ __
  / __| | |/ _ \ '__| |/ _ \ '__|
 | (__| | |  __/ |  | |  __/ |
  \___|_|_|\___|_|  |_|\___|_|
```

# Burcin [![](https://camo.githubusercontent.com/5a11fc143b729c5d9dfd8a88097be39354fd9230/68747470733a2f2f6261646765732e6769747465722e696d2f63696c65726c65722d62757263696e2f4c6f6262792e737667)](https://gitter.im/cilerler-burcin/Lobby?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=body_badge) [![Build status](https://ci.appveyor.com/api/projects/status/607wc5eksiusq4jl?svg=true)](https://ci.appveyor.com/project/cilerler/burcin) [![](https://ilerler.visualstudio.com/_apis/public/build/definitions/94517f08-14c6-4500-af55-611a030525e3/50/badge)](https://ilerler.visualstudio.com/Burcin/_build) [![](https://img.shields.io/badge/stackoverflow-burcin-orange.svg)](https://stackoverflow.com/questions/tagged/burcin)

[![](https://img.shields.io/nuget/v/Burcin.Templates.CSharp.svg)](https://www.nuget.org/packages/Burcin.Templates.CSharp)
![](https://img.shields.io/nuget/dt/Burcin.Templates.CSharp.svg)

![](https://img.shields.io/github/release/cilerler/burcin.svg)
![](https://img.shields.io/github/downloads/cilerler/burcin/latest/total.svg)


All `Burcin` words under `dist` folder will be changed to the folder name

## Install

```
dotnet new --install "Burcin.Templates.CSharp"
```

## Uninstall
```
dotnet new --uninstall "Burcin.Templates.CSharp"
```

## Help

```
dotnet new burcin --help
```

## Run
```
dotnet new burcin --ConsoleApplication --BackgroundService --HealthChecks --EntityFramework --TestFramework --DockerSupport --Swagger --VsCodeDirectory --GithubTemplates --Cache "All" --DatabaseName "BurcinDb" --Authors "Cengiz Ilerler" --RepositoryUrl "https://github.com/cilerler/burcin" --SkipRestore;
```