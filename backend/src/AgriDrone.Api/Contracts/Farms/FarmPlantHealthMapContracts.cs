namespace AgriDrone.Api.Contracts.Farms;

public sealed record FarmPlantHealthMapResponse(
    Guid FarmId,
    Guid TenantId,
    BaseMapInfoResponse? BaseMap,
    PlantHealthSummaryResponse Summary,
    IReadOnlyList<PlantHealthItemResponse> Plants,
    string? BoundaryGeoJson = null,
    IReadOnlyList<DiseaseZoneItemResponse>? Zones = null);

public sealed record DiseaseZoneItemResponse(
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
    IReadOnlyList<DiseaseZoneTreeItemResponse> Trees);

public sealed record DiseaseZoneTreeItemResponse(
    Guid PlantId,
    string PlantCode,
    int Row,
    int Col,
    double Lat,
    double Lon);

public sealed record BaseMapInfoResponse(
    Guid SourceMissionId,
    string Status,
    string? PreviewUrl,
    MapBoundsResponse? Bounds,
    double GridBearingDeg,
    double PlantSpacingM,
    double RowSpacingM);

public sealed record MapBoundsResponse(
    double MinLat,
    double MaxLat,
    double MinLon,
    double MaxLon);

public sealed record PlantHealthSummaryResponse(
    int TotalPlants,
    int NormalPlants,
    int HealthyFruitPlants,
    int DiseasePlants,
    IReadOnlyDictionary<string, int> DiseaseBreakdown);

public sealed record PlantHealthItemResponse(
    Guid Id,
    string PlantCode,
    int Row,
    int Col,
    double Lat,
    double Lon,
    string LifecycleStatus,
    string HealthStatus,
    IReadOnlyList<PlantObservationItemResponse> Observations);

public sealed record PlantObservationItemResponse(
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

