FROM mcr.microsoft.com/dotnet/core/aspnet:3.0-buster-slim AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/core/sdk:3.0-buster AS build
WORKDIR /src
COPY ["nuget.config","src/"]
COPY ["src/Burcin.Api/Burcin.Api.csproj","src/Burcin.Api/"]
RUN dotnet restore "src/Burcin.Api/Burcin.Api.csproj"
COPY . .
WORKDIR "/src/src/Burcin.Api"
RUN dotnet build "Burcin.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Burcin.Api.csproj" -c Release -o /app/publish /p:BuildNumber=$(date "+%y.%m%d.%H%M")

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Burcin.Api.dll"]
