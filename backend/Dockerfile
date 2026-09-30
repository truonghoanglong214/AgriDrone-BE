FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /workspace
COPY . .
RUN dotnet restore AgriDrone.sln
RUN dotnet publish src/AgriDrone.Api/AgriDrone.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "AgriDrone.Api.dll"]
