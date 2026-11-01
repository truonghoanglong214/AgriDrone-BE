using System.Text.Json;
using AgriDrone.Modules.Missions.Application.Abstractions.Processing;

namespace AgriDrone.Api.Contracts.Missions;

public sealed record SliceMissionTilesRequest(
    int TileSize = 640,
    int Overlap = 64,
    bool DualScale = true,
    JsonElement? PolygonGeoJson = null,
    string? TifStorageKey = null,
    bool UploadToMinio = false);

public sealed record MissionTileItemResponse(
    int TileIndex,
    int? TileSize,
    int ColOff,
    int RowOff,
    int Width,
    int Height,
    string FileName,
    string? StorageKey,
    string? Url,
    MapBounds Bounds,
    MapCorners Corners,
    IReadOnlyList<double> AffineTransform);

public sealed record SliceMissionTilesResponse(
    Guid MissionId,
    int TotalTiles,
    int TotalTiles640,
    int TotalTiles1280,
    bool DualScale,
    IReadOnlyList<MissionTileItemResponse> Tiles);
