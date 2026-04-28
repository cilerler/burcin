# #See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER app
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
ARG VERSION
#--if (GitHubTemplates)
RUN --mount=type=secret,id=GITHUB_PAT \
    dotnet nuget add source https://nuget.pkg.github.com/burcinco/index.json -n "github" -u "docker" -p "$(cat /run/secrets/GITHUB_PAT)" --store-password-in-clear-text
#--endif
WORKDIR /build
COPY ["Directory.Packages.props", "./"]
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Build.targets", "./"]
COPY ["global.json", "./"]
COPY ["nuget.config","./"]
COPY ["src/BurcinCo.BurcinApp.Models/BurcinCo.BurcinApp.Models.csproj","src/BurcinCo.BurcinApp.Models/"]
COPY ["src/BurcinCo.BurcinApp.Data/BurcinCo.BurcinApp.Data.csproj","src/BurcinCo.BurcinApp.Data/"]
COPY ["src/BurcinCo.BurcinApp.Domain/BurcinCo.BurcinApp.Domain.csproj","src/BurcinCo.BurcinApp.Domain/"]
COPY ["src/BurcinCo.BurcinApp.Migrations/BurcinCo.BurcinApp.Migrations.csproj","src/BurcinCo.BurcinApp.Migrations/"]
COPY ["src/BurcinCo.BurcinApp.Host/BurcinCo.BurcinApp.Host.csproj","src/BurcinCo.BurcinApp.Host/"]
RUN dotnet restore "./src/BurcinCo.BurcinApp.Host/BurcinCo.BurcinApp.Host.csproj" -p:Configuration=Release
COPY . .
WORKDIR "/build/src/BurcinCo.BurcinApp.Host"

FROM build AS publish
RUN dotnet publish "./BurcinCo.BurcinApp.Host.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false /p:Version=${VERSION:-$(date "+%y.%m%d.%H%M")}

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "BurcinCo.BurcinApp.Host.dll"]
