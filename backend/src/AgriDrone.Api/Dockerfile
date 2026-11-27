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
ENV Jwt__Secret="BuildTimePlaceholderSecretKeyDoNotUseInProd1234567890!"
RUN dotnet ef migrations bundle \
    --project src/BuildingBlocks/AgriDrone.Database/AgriDrone.Database.csproj \
    --startup-project src/AgriDrone.Api/AgriDrone.Api.csproj \
    --context AgriDroneSchemaDbContext \
    --output /app/publish-migrator/efbundle \
    --configuration Release \
    --self-contained \
    -r linux-x64 \
    --no-build

# Install the official INAV decoder for the target architecture.
FROM debian:bookworm-slim AS blackbox-decoder
ARG TARGETARCH
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates curl zstd \
    && rm -rf /var/lib/apt/lists/*
RUN case "$TARGETARCH" in \
        amd64) archive_arch=x64_64; sha256=8aaa56c319ad1c614635331ed1246ff293fcd55d548783124ce521cc14a8e05d ;; \
        arm64) archive_arch=aarch64; sha256=ad1442449186a7372e60f277a69787ca91d5b00c8d23eb7417d88644b36edc37 ;; \
        *) echo "Unsupported INAV decoder architecture: $TARGETARCH" >&2; exit 1 ;; \
    esac \
    && curl -fsSL --retry 3 "https://github.com/iNavFlight/blackbox-tools/releases/download/v9.0.0/blackbox-tools-9.0.0_linux-${archive_arch}.tar.zst" -o /tmp/blackbox-tools.tar.zst \
    && echo "$sha256  /tmp/blackbox-tools.tar.zst" | sha256sum --check \
    && tar --zstd -xf /tmp/blackbox-tools.tar.zst -C /tmp ./bin/blackbox_decode \
    && install -m 0755 /tmp/bin/blackbox_decode /usr/local/bin/blackbox_decode

# Stage 2: Production API Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install curl for healthcheck
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=blackbox-decoder /usr/local/bin/blackbox_decode /usr/local/bin/blackbox_decode

USER $APP_UID

RUN ["/usr/local/bin/blackbox_decode", "--version"]

COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

HEALTHCHECK --interval=15s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/health/live || exit 1

# Apply the image's decoder settings even if an older Compose env file supplies overrides.
ENTRYPOINT ["env", \
    "Telemetry__BlackboxDecoder__Enabled=true", \
    "Telemetry__BlackboxDecoder__ExecutablePath=/usr/local/bin/blackbox_decode", \
    "dotnet", "AgriDrone.Api.dll"]

# Stage 3: One-shot Database Migrator
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS migrator
WORKDIR /app

USER $APP_UID

COPY --from=build /app/publish-migrator/efbundle ./
COPY src/AgriDrone.Api/appsettings.json ./

ENTRYPOINT ["./efbundle"]
