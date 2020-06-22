FROM mcr.microsoft.com/dotnet/core/aspnet:3.1.2-buster-slim AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
WORKDIR /src
COPY ["nuget.config","src/"]
COPY ["src/Burcin.Host/Burcin.Host.csproj","src/Burcin.Host/"]
RUN dotnet restore "src/Burcin.Host/Burcin.Host.csproj"
COPY . .
WORKDIR "/src/src/Burcin.Host"
RUN dotnet build "Burcin.Host.csproj" -c Release -o /app/build

FROM build AS publish
ARG BUILDNUMBER
ENV BuildNumber=${BUILDNUMBER}
RUN dotnet publish "Burcin.Host.csproj" -c Release -o /app/publish /p:BuildNumber=/p:BuildNumber=${BuildNumber:-$(date "+%y.%m%d.%H%M")}

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Burcin.Host.dll"]
