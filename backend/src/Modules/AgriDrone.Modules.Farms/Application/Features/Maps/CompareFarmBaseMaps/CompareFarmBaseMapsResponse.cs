namespace AgriDrone.Modules.Farms.Application.Features.Maps.CompareFarmBaseMaps;

public sealed record GeoCoordinateDto(double Lat, double Lon);

public sealed record ComparePoleChangeItemDto(
    Guid? PlantId,
    string PlantCode,
    string ChangeType, // "CREATED", "REMOVED", "RELOCATED", "REPLACED"
    int Row,
    int Col,
    GeoCoordinateDto? NewPosition,
    GeoCoordinateDto? OldPosition,
    double? DistanceMovedM,
    string? Notes);

public sealed record CompareSummaryDto(
    int PolesBefore,
    int PolesAfter,
    int NetChange,
    int CreatedCount,
    int RemovedCount,
    int RelocatedCount,
    int ReplacedCount,
    int UnchangedCount);

public sealed record CompareFarmBaseMapsResponse(
    Guid FarmId,
    int FromVersionNumber,
    Guid FromVersionId,
    DateTimeOffset FromVersionDate,
    int ToVersionNumber,
    Guid ToVersionId,
    DateTimeOffset ToVersionDate,
    CompareSummaryDto Summary,
    IReadOnlyList<ComparePoleChangeItemDto> Changes);

