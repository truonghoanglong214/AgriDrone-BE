namespace AgriDrone.Modules.Missions.Application.Features.Processing.GetFarmPlantHealthMap;

public sealed record FarmPlantHealthMapResult(
    Guid FarmId,
    Guid TenantId,
    BaseMapInfoResult? BaseMap,
    PlantHealthSummaryResult Summary,
    IReadOnlyList<PlantHealthItemResult> Plants,
    string? BoundaryGeoJson = null,
    IReadOnlyList<DiseaseZoneItemResult>? Zones = null);

public sealed record DiseaseZoneItemResult(
    Guid Id,
    Guid ZoneKey,
    string Status,
    string AlertType,
    string? DiseaseName,
    string? DiseaseNameVi,
    int TotalTrees,
    double CenterLat,
    double CenterLon,
    double RadiusMeters,
    string? GeometryGeoJson,
    IReadOnlyDictionary<string, int> DiseaseBreakdown,
    IReadOnlyList<DiseaseZoneTreeItemResult> Trees);

public sealed record DiseaseZoneTreeItemResult(
    Guid PlantId,
    string PlantCode,
    int Row,
    int Col,
    double Lat,
    double Lon);

public sealed record BaseMapInfoResult(
    Guid SourceMissionId,
    string Status,
    string? PreviewUrl,
    MapBoundsResult? Bounds,
    double GridBearingDeg,
    double PlantSpacingM,
    double RowSpacingM);

public sealed record MapBoundsResult(
    double MinLat,
    double MaxLat,
    double MinLon,
    double MaxLon);

public sealed record PlantHealthSummaryResult(
    int TotalPlants,
    int NormalPlants,
    int HealthyFruitPlants,
    int DiseasePlants,
    IReadOnlyDictionary<string, int> DiseaseBreakdown);

public sealed record PlantHealthItemResult(
    Guid Id,
    string PlantCode,
    int Row,
    int Col,
    double Lat,
    double Lon,
    string LifecycleStatus,
    string HealthStatus,
    IReadOnlyList<PlantObservationItemResult> Observations);

public sealed record PlantObservationItemResult(
    Guid Id,
    Guid MissionId,
    string ClassName,
    string ClassNameVi,
    double Confidence,
    double GpsDistanceM,
    string? ImageName,
    string? CropImageUrl,
    string? AnnotatedImageUrl,
    string? DetectionId);

