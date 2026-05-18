FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER app
WORKDIR /app
EXPOSE 80
EXPOSE 443

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
COPY ["src/BurcinCo.BurcinApp.Gateway/BurcinCo.BurcinApp.Gateway.csproj","src/BurcinCo.BurcinApp.Gateway/"]
RUN dotnet restore "./src/BurcinCo.BurcinApp.Gateway/BurcinCo.BurcinApp.Gateway.csproj" -p:Configuration=Release
COPY . .
WORKDIR "/build/src/BurcinCo.BurcinApp.Gateway"

FROM build AS publish
RUN dotnet publish "./BurcinCo.BurcinApp.Gateway.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false /p:Version=${VERSION:-$(date "+%y.%m%d.%H%M")}

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "BurcinCo.BurcinApp.Gateway.dll"]
