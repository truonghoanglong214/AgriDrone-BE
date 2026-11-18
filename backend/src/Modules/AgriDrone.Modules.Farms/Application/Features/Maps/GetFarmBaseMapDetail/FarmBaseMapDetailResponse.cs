namespace AgriDrone.Modules.Farms.Application.Features.Maps.GetFarmBaseMapDetail;

public sealed record MapBoundsDto(
    double MinLat,
    double MaxLat,
    double MinLon,
    double MaxLon);

public sealed record BaseMapPlantItemDto(
    Guid PlantId,
    string PlantCode,
    int Row,
    int Col,
    double Lat,
    double Lon,
    string LifecycleStatus);

public sealed record BaseMapManualChangeDto(
    Guid EventId,
    Guid? PlantId,
    string ChangeType,
    string Source,
    int? OldRow,
    int? NewRow,
    int? OldCol,
    int? NewCol,
    double? OldLat,
    double? OldLon,
    double? NewLat,
    double? NewLon,
    string? OldLifecycleStatus,
    string? NewLifecycleStatus,
    string? Notes,
    DateTimeOffset CreatedAt);

public sealed record FarmBaseMapDetailResponse(
    Guid VersionId,
    Guid FarmId,
    int VersionNumber,
    string Status,
    Guid? SourceMissionId,
    string? SourceMissionCode,
    DateTimeOffset? FlightDate,
    Guid? SourceSurveyOrderId,
    Guid? FarmBoundaryVersionId,
    string? BoundaryGeoJson,
    string? PreviewUrl,
    MapBoundsDto? Bounds,
    double GridBearingDeg,
    double RowSpacingM,
    double PlantSpacingM,
    int TotalPoles,
    DateTimeOffset? PublishedAt,
    Guid? PublishedBy,
    DateTimeOffset CreatedAt,
    IReadOnlyList<BaseMapPlantItemDto> Plants,
    IReadOnlyList<BaseMapManualChangeDto>? ManualChanges = null);

