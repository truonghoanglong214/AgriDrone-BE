FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /workspace
COPY . .
RUN dotnet restore tests/AgriDrone.Gateway.Tests/AgriDrone.Gateway.Tests.csproj
RUN dotnet restore src/AgriDrone.Gateway/AgriDrone.Gateway.csproj
RUN dotnet test tests/AgriDrone.Gateway.Tests/AgriDrone.Gateway.Tests.csproj \
    --configuration Release \
    --no-restore
RUN dotnet publish src/AgriDrone.Gateway/AgriDrone.Gateway.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "AgriDrone.Gateway.dll"]
