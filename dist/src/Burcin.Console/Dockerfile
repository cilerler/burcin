FROM microsoft/dotnet:2.1-runtime AS base
WORKDIR /app

FROM microsoft/dotnet:2.1-sdk AS build
WORKDIR /src
COPY Burcin.Console.sln ./
COPY Burcin.Console/Burcin.Console.csproj Burcin.Console/
RUN dotnet restore -nowarn:msb3202,nu1503
COPY . .
WORKDIR /src/Burcin.Console
RUN dotnet build -c Release -o /app

FROM build AS publish
RUN dotnet publish -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "Burcin.Console.dll"]
