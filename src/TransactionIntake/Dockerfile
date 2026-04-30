FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/BuildingBlocks/Contracts/Contracts.csproj             src/BuildingBlocks/Contracts/
COPY src/BuildingBlocks/Observability/Observability.csproj     src/BuildingBlocks/Observability/
COPY src/TransactionIntake/TransactionIntake.Domain/TransactionIntake.Domain.csproj             src/TransactionIntake/TransactionIntake.Domain/
COPY src/TransactionIntake/TransactionIntake.Application/TransactionIntake.Application.csproj   src/TransactionIntake/TransactionIntake.Application/
COPY src/TransactionIntake/TransactionIntake.Infrastructure/TransactionIntake.Infrastructure.csproj src/TransactionIntake/TransactionIntake.Infrastructure/
COPY src/TransactionIntake/TransactionIntake.Api/TransactionIntake.Api.csproj                   src/TransactionIntake/TransactionIntake.Api/

RUN dotnet restore src/TransactionIntake/TransactionIntake.Api/TransactionIntake.Api.csproj

COPY src/BuildingBlocks/ src/BuildingBlocks/
COPY src/TransactionIntake/ src/TransactionIntake/

RUN dotnet publish src/TransactionIntake/TransactionIntake.Api/TransactionIntake.Api.csproj \
    -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PlatformWallet.TransactionIntake.Api.dll"]
