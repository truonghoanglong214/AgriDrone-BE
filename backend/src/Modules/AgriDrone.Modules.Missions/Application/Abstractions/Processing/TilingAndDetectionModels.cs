namespace AgriDrone.Modules.Missions.Application.Abstractions.Processing;

public sealed record TileSliceItem(
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

public sealed record SliceTilesResult(
    bool IsSuccess,
    Guid MissionId,
    int TotalTiles,
    int TotalTiles640,
    int TotalTiles1280,
    bool DualScale,
    IReadOnlyList<TileSliceItem> Tiles,
    string? ErrorMessage = null);

public sealed record TreeDetectionItem(
    string Id,
    int Row,
    int Col,
    double Lat,
    double Lon,
    double Confidence,
    string Status,
    string Source,
    bool IsAnchor = false);

public sealed record DetectMissionTreesResult(
    bool IsSuccess,
    Guid MissionId,
    string ModelName,
    int TotalTilesScanned,
    int TotalRawDetections,
    int TotalUniqueTrees,
    int TotalMissingTrees,
    double? SuggestedRowSpacing,
    double? SuggestedColSpacing,
    int MaxRow,
    int MaxCol,
    string? CsvDownloadUrl,
    IReadOnlyList<TreeDetectionItem> ActiveTrees,
    IReadOnlyList<TreeDetectionItem> MissingTrees,
    string? ErrorMessage = null);
