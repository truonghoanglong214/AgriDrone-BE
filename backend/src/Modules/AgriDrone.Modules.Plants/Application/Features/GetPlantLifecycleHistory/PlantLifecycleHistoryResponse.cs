namespace AgriDrone.Modules.Plants.Application.Features.GetPlantLifecycleHistory;

public sealed record GeoPointDto(double Lat, double Lon);

public sealed record PlantLifecycleEventItemDto(
    Guid EventId,
    string ChangeType,
    string Source,
    string Summary,
    GeoPointDto? OldPosition,
    GeoPointDto? NewPosition,
    int? OldRow,
    int? NewRow,
    int? OldCol,
    int? NewCol,
    string? Notes,
    Guid? MissionId,
    string? MissionCode,
    Guid? CreatedBy,
    DateTimeOffset CreatedAt,
    Guid? FarmBaseMapVersionId = null);

public sealed record PlantLifecycleHistoryResponse(
    Guid PlantId,
    Guid FarmId,
    string PlantCode,
    GeoPointDto? CurrentLocation,
    int? CurrentRow,
    int? CurrentCol,
    string LifecycleStatus,
    Guid? CurrentMapVersionId,
    DateTimeOffset? MappedAt,
    DateTimeOffset CreatedAt,
    IReadOnlyList<PlantLifecycleEventItemDto> Timeline);

