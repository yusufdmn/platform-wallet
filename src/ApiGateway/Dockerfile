FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/BuildingBlocks/Observability/Observability.csproj     src/BuildingBlocks/Observability/
COPY src/ApiGateway/ApiGateway.Yarp.csproj                     src/ApiGateway/

RUN dotnet restore src/ApiGateway/ApiGateway.Yarp.csproj

COPY src/BuildingBlocks/ src/BuildingBlocks/
COPY src/ApiGateway/     src/ApiGateway/

RUN dotnet publish src/ApiGateway/ApiGateway.Yarp.csproj \
    -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PlatformWallet.ApiGateway.Yarp.dll"]
