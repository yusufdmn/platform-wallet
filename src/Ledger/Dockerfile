FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/BuildingBlocks/Contracts/Contracts.csproj             src/BuildingBlocks/Contracts/
COPY src/BuildingBlocks/Grpc.Protos/Grpc.Protos.csproj         src/BuildingBlocks/Grpc.Protos/
COPY src/BuildingBlocks/Observability/Observability.csproj     src/BuildingBlocks/Observability/
COPY src/Ledger/Ledger.Domain/Ledger.Domain.csproj             src/Ledger/Ledger.Domain/
COPY src/Ledger/Ledger.Application/Ledger.Application.csproj   src/Ledger/Ledger.Application/
COPY src/Ledger/Ledger.Infrastructure/Ledger.Infrastructure.csproj src/Ledger/Ledger.Infrastructure/
COPY src/Ledger/Ledger.Api/Ledger.Api.csproj                   src/Ledger/Ledger.Api/

RUN dotnet restore src/Ledger/Ledger.Api/Ledger.Api.csproj

COPY src/BuildingBlocks/ src/BuildingBlocks/
COPY src/Ledger/         src/Ledger/

RUN dotnet publish src/Ledger/Ledger.Api/Ledger.Api.csproj \
    -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PlatformWallet.Ledger.Api.dll"]
