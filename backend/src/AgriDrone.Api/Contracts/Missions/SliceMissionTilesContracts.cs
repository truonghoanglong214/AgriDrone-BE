using System.Text.Json;

namespace AgriDrone.Api.Contracts.Missions;

public sealed record SliceMissionTilesRequest(
    int TileSize = 640,
    int Overlap = 64,
    bool DualScale = true,
    JsonElement? PolygonGeoJson = null,
    string? TifStorageKey = null,
    bool UploadToMinio = false);
