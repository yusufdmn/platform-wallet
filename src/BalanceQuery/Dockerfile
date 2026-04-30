FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/BuildingBlocks/Contracts/Contracts.csproj             src/BuildingBlocks/Contracts/
COPY src/BuildingBlocks/Grpc.Protos/Grpc.Protos.csproj         src/BuildingBlocks/Grpc.Protos/
COPY src/BuildingBlocks/Observability/Observability.csproj     src/BuildingBlocks/Observability/
COPY src/BalanceQuery/BalanceQuery.Domain/BalanceQuery.Domain.csproj             src/BalanceQuery/BalanceQuery.Domain/
COPY src/BalanceQuery/BalanceQuery.Application/BalanceQuery.Application.csproj   src/BalanceQuery/BalanceQuery.Application/
COPY src/BalanceQuery/BalanceQuery.Infrastructure/BalanceQuery.Infrastructure.csproj src/BalanceQuery/BalanceQuery.Infrastructure/
COPY src/BalanceQuery/BalanceQuery.Api/BalanceQuery.Api.csproj                   src/BalanceQuery/BalanceQuery.Api/

RUN dotnet restore src/BalanceQuery/BalanceQuery.Api/BalanceQuery.Api.csproj

COPY src/BuildingBlocks/ src/BuildingBlocks/
COPY src/BalanceQuery/   src/BalanceQuery/

RUN dotnet publish src/BalanceQuery/BalanceQuery.Api/BalanceQuery.Api.csproj \
    -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PlatformWallet.BalanceQuery.Api.dll"]
