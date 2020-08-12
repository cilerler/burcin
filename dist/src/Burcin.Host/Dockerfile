#--if (WebApiApplicationExists)
FROM mcr.microsoft.com/dotnet/core/aspnet:3.1.6-buster-slim AS base
#--else
FROM mcr.microsoft.com/dotnet/core/runtime:3.1-buster-slim AS base
#--endif
WORKDIR /app

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
RUN dotnet publish "Burcin.Host.csproj" -c Release -o /app/publish /p:BuildNumber=${BuildNumber:-$(date "+%y.%m%d.%H%M")}

FROM base AS final
# ENV DOTNET_USE_POLLING_FILE_WATCHER=true
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Burcin.Host.dll"]

#--if (WebApiApplicationExists)
EXPOSE 80
EXPOSE 443
#--endif

