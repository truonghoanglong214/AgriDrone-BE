using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Farms.Application.Abstractions.Queries;

public interface IFarmOperationalContextQueries
{
    Task<FarmOperationalContextDto?> GetAssignedFarmOperationalContextAsync(
        Guid tenantId,
        Guid farmId,
        CancellationToken cancellationToken = default);
}

public sealed record FarmOperationalContextDto(
    Guid FarmId,
    Guid TenantId,
    string Code,
    string Name,
    string? Address,
    string Status,
    decimal? AreaHectares,
    long Version,
    ApprovedBoundaryDto? ApprovedBoundary,
    IReadOnlyList<OperationalZoneDto> Zones,
    PublishedBaseMapSummaryDto? PublishedBaseMap);

public sealed record ApprovedBoundaryDto(
    Guid Id,
    int VersionNumber,
    Polygon Geometry,
    string Source,
    string? EvidenceReference,
    Guid? ReviewedBy,
    DateTimeOffset? ReviewedAt,
    string? ReviewReason,
    uint Version);

public sealed record OperationalZoneDto(
    Guid Id,
    string Code,
    string Name,
    string Status,
    decimal? AreaHectares,
    Polygon? Boundary,
    long Version);

public sealed record PublishedBaseMapSummaryDto(
    Guid Id,
    int VersionNumber,
    Guid SourceSurveyOrderId,
    DateTimeOffset? PublishedAt,
    Guid? PublishedBy,
    uint Version);
