#--if (WebApiApplicationExists)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
#--else
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS base
#--endif
WORKDIR /app
#--if (WebApiApplicationExists)
EXPOSE 80
EXPOSE 443
#--endif

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
ENV BUILD_CONFIGURATION=${BUILD_CONFIGURATION}
#--if (GitHubTemplates)
ARG GITHUB_PAT
RUN dotnet nuget add source https://nuget.pkg.github.com/burcin/index.json -n "github" -u "docker" -p "${GITHUB_PAT}" --store-password-in-clear-text
#--endif
WORKDIR /src
COPY ["nuget.config","src/"]
COPY ["src/Burcin.Host/Burcin.Host.csproj","src/Burcin.Host/"]
RUN dotnet restore "src/Burcin.Host/Burcin.Host.csproj"
COPY . .
WORKDIR "/src/src/Burcin.Host"
RUN dotnet build "Burcin.Host.csproj" -c ${BUILD_CONFIGURATION} -o /app/build

FROM build AS publish
ARG VERSION
ENV Version=${VERSION}
RUN dotnet publish "Burcin.Host.csproj" -c ${BUILD_CONFIGURATION} -o /app/publish /p:Version=${Version:-$(date "+%y.%m%d.%H%M")}

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Burcin.Host.dll"]


