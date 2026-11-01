using System.Text.Json;

namespace AgriDrone.Api.Contracts.Missions;

public sealed record DetectMissionTreesRequest(
    double ConfidenceThreshold = 0.25,
    double RowSpacing = 2.8,
    double ColSpacing = 2.5,
    double? AnchorLat = null,
    double? AnchorLon = null,
    double? CustomAngleDeg = null,
    JsonElement? PolygonGeoJson = null);

public sealed record DetectedTreeItemResponse(
    string Id,
    int Row,
    int Col,
    double Lat,
    double Lon,
    double Confidence,
    string Status,
    string Source,
    bool IsAnchor = false);

public sealed record DetectMissionTreesResponse(
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
    IReadOnlyList<DetectedTreeItemResponse> ActiveTrees,
    IReadOnlyList<DetectedTreeItemResponse> MissingTrees);
