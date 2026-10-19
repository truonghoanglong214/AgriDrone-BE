using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Farms.Application.Features.GetAssignedFarmOperationalContext;

public sealed record FarmOperationalContextResponse(
    Guid FarmId,
    Guid TenantId,
    string Code,
    string Name,
    string? Address,
    string Status,
    decimal? AreaHectares,
    long Version,
    ApprovedBoundarySummaryResponse? ApprovedBoundary,
    IReadOnlyList<OperationalZoneSummaryResponse> Zones,
    PublishedBaseMapSummaryResponse? PublishedBaseMap);

public sealed record ApprovedBoundarySummaryResponse(
    Guid Id,
    int VersionNumber,
    Polygon Geometry,
    string Source,
    string? EvidenceReference,
    Guid? ReviewedBy,
    DateTimeOffset? ReviewedAt,
    string? ReviewReason,
    uint Version);

public sealed record OperationalZoneSummaryResponse(
    Guid Id,
    string Code,
    string Name,
    string Status,
    decimal? AreaHectares,
    Polygon? Boundary,
    long Version);

public sealed record PublishedBaseMapSummaryResponse(
    Guid Id,
    int VersionNumber,
    Guid SourceSurveyOrderId,
    DateTimeOffset? PublishedAt,
    Guid? PublishedBy,
    uint Version);
