# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Copy props and tool manifests
COPY Directory.Build.props Directory.Packages.props ./
COPY .config/dotnet-tools.json ./.config/

# Copy all src csproj files for optimal layer caching
COPY src/AgriDrone.Api/AgriDrone.Api.csproj src/AgriDrone.Api/
COPY src/BuildingBlocks/AgriDrone.Database/AgriDrone.Database.csproj src/BuildingBlocks/AgriDrone.Database/
COPY src/BuildingBlocks/AgriDrone.IntegrationContracts/AgriDrone.IntegrationContracts.csproj src/BuildingBlocks/AgriDrone.IntegrationContracts/
COPY src/BuildingBlocks/AgriDrone.SharedInfrastructure/AgriDrone.SharedInfrastructure.csproj src/BuildingBlocks/AgriDrone.SharedInfrastructure/
COPY src/BuildingBlocks/AgriDrone.SharedKernel/AgriDrone.SharedKernel.csproj src/BuildingBlocks/AgriDrone.SharedKernel/
COPY src/Integrations/AgriDrone.Integrations.AI/AgriDrone.Integrations.AI.csproj src/Integrations/AgriDrone.Integrations.AI/
COPY src/Integrations/AgriDrone.Integrations.Email/AgriDrone.Integrations.Email.csproj src/Integrations/AgriDrone.Integrations.Email/
COPY src/Integrations/AgriDrone.Integrations.Media/AgriDrone.Integrations.Media.csproj src/Integrations/AgriDrone.Integrations.Media/
COPY src/Integrations/AgriDrone.Integrations.SePay/AgriDrone.Integrations.SePay.csproj src/Integrations/AgriDrone.Integrations.SePay/
COPY src/Modules/AgriDrone.Modules.Farms/AgriDrone.Modules.Farms.csproj src/Modules/AgriDrone.Modules.Farms/
COPY src/Modules/AgriDrone.Modules.Identity/AgriDrone.Modules.Identity.csproj src/Modules/AgriDrone.Modules.Identity/
COPY src/Modules/AgriDrone.Modules.Missions/AgriDrone.Modules.Missions.csproj src/Modules/AgriDrone.Modules.Missions/
COPY src/Modules/AgriDrone.Modules.Notifications/AgriDrone.Modules.Notifications.csproj src/Modules/AgriDrone.Modules.Notifications/
COPY src/Modules/AgriDrone.Modules.Plants/AgriDrone.Modules.Plants.csproj src/Modules/AgriDrone.Modules.Plants/
COPY src/Modules/AgriDrone.Modules.Surveys/AgriDrone.Modules.Surveys.csproj src/Modules/AgriDrone.Modules.Surveys/

# Restore packages
RUN dotnet restore src/AgriDrone.Api/AgriDrone.Api.csproj

# Restore dotnet local tools (including pinned dotnet-ef)
RUN dotnet tool restore

# Copy all source files
COPY src/ src/

# Publish API
RUN dotnet publish src/AgriDrone.Api/AgriDrone.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Build self-contained EF migration bundle for Linux x64
RUN dotnet ef migrations bundle \
    --project src/BuildingBlocks/AgriDrone.Database/AgriDrone.Database.csproj \
    --startup-project src/AgriDrone.Api/AgriDrone.Api.csproj \
    --output /app/publish-migrator/efbundle \
    --configuration Release \
    --self-contained \
    -r linux-x64 \
    --no-build

# Stage 2: Production API Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install curl for healthcheck
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

USER $APP_UID

COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

HEALTHCHECK --interval=15s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "AgriDrone.Api.dll"]

# Stage 3: One-shot Database Migrator
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS migrator
WORKDIR /app

USER $APP_UID

COPY --from=build /app/publish-migrator/efbundle ./
COPY src/AgriDrone.Api/appsettings.json ./

ENTRYPOINT ["./efbundle"]
