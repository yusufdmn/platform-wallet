FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/BuildingBlocks/Contracts/Contracts.csproj             src/BuildingBlocks/Contracts/
COPY src/BuildingBlocks/Observability/Observability.csproj     src/BuildingBlocks/Observability/
COPY src/SagaOrchestrator/SagaOrchestrator.Domain/SagaOrchestrator.Domain.csproj             src/SagaOrchestrator/SagaOrchestrator.Domain/
COPY src/SagaOrchestrator/SagaOrchestrator.Application/SagaOrchestrator.Application.csproj   src/SagaOrchestrator/SagaOrchestrator.Application/
COPY src/SagaOrchestrator/SagaOrchestrator.Infrastructure/SagaOrchestrator.Infrastructure.csproj src/SagaOrchestrator/SagaOrchestrator.Infrastructure/
COPY src/SagaOrchestrator/SagaOrchestrator.Worker/SagaOrchestrator.Worker.csproj             src/SagaOrchestrator/SagaOrchestrator.Worker/

RUN dotnet restore src/SagaOrchestrator/SagaOrchestrator.Worker/SagaOrchestrator.Worker.csproj

COPY src/BuildingBlocks/    src/BuildingBlocks/
COPY src/SagaOrchestrator/  src/SagaOrchestrator/

RUN dotnet publish src/SagaOrchestrator/SagaOrchestrator.Worker/SagaOrchestrator.Worker.csproj \
    -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PlatformWallet.SagaOrchestrator.Worker.dll"]
