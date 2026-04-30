FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/BuildingBlocks/Contracts/Contracts.csproj             src/BuildingBlocks/Contracts/
COPY src/BuildingBlocks/Observability/Observability.csproj     src/BuildingBlocks/Observability/
COPY src/WebhookDispatcher/WebhookDispatcher.Domain/WebhookDispatcher.Domain.csproj             src/WebhookDispatcher/WebhookDispatcher.Domain/
COPY src/WebhookDispatcher/WebhookDispatcher.Application/WebhookDispatcher.Application.csproj   src/WebhookDispatcher/WebhookDispatcher.Application/
COPY src/WebhookDispatcher/WebhookDispatcher.Infrastructure/WebhookDispatcher.Infrastructure.csproj src/WebhookDispatcher/WebhookDispatcher.Infrastructure/
COPY src/WebhookDispatcher/WebhookDispatcher.Worker/WebhookDispatcher.Worker.csproj             src/WebhookDispatcher/WebhookDispatcher.Worker/

RUN dotnet restore src/WebhookDispatcher/WebhookDispatcher.Worker/WebhookDispatcher.Worker.csproj

COPY src/BuildingBlocks/     src/BuildingBlocks/
COPY src/WebhookDispatcher/  src/WebhookDispatcher/

RUN dotnet publish src/WebhookDispatcher/WebhookDispatcher.Worker/WebhookDispatcher.Worker.csproj \
    -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PlatformWallet.WebhookDispatcher.Worker.dll"]
