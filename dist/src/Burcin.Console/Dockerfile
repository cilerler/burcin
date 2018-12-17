FROM microsoft/dotnet:2.2-runtime AS base
WORKDIR /app

FROM microsoft/dotnet:2.2-sdk AS build
WORKDIR /src
COPY nuget.config src/
COPY src/Burcin.Console/Burcin.Console.csproj src/Burcin.Console/
RUN dotnet restore src/Burcin.Console/Burcin.Console.csproj
COPY . .
WORKDIR /src/src/Burcin.Console
RUN dotnet build Burcin.Console.csproj -c Release -o /app

FROM build AS publish
RUN dotnet publish Burcin.Console.csproj -c Release -o /app /p:BuildNumber=$(date "+%y.%m%d.%H%M")

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "Burcin.Console.dll"]
