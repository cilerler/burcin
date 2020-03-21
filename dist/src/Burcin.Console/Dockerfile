FROM mcr.microsoft.com/dotnet/core/runtime:3.1.2-buster-slim AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/core/sdk:3.1.2-buster AS build
WORKDIR /src
COPY ["nuget.config","src/"]
COPY ["src/Burcin.Console/Burcin.Console.csproj","src/Burcin.Console/"]
RUN dotnet restore "src/Burcin.Console/Burcin.Console.csproj"
COPY . .
WORKDIR "/src/src/Burcin.Console"
RUN dotnet build "Burcin.Console.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Burcin.Console.csproj" -c Release -o /app/publish /p:BuildNumber=$(date "+%y.%m%d.%H%M")

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Burcin.Console.dll"]
