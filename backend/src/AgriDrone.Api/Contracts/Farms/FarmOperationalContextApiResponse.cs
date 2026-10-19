namespace AgriDrone.Api.Contracts.Farms;

public sealed record FarmOperationalContextApiResponse(
    Guid FarmId,
    Guid TenantId,
    string Code,
    string Name,
    string? Address,
    string Status,
    decimal? AreaHectares,
    long Version,
    ApprovedBoundaryApiResponse? ApprovedBoundary,
    IReadOnlyList<OperationalZoneApiResponse> Zones,
    PublishedBaseMapApiResponse? PublishedBaseMap);

public sealed record ApprovedBoundaryApiResponse(
    Guid Id,
    int VersionNumber,
    GeoJsonPolygonResponse? Geometry,
    string Source,
    string? EvidenceReference,
    Guid? ReviewedBy,
    DateTimeOffset? ReviewedAt,
    string? ReviewReason,
    uint Version);

public sealed record OperationalZoneApiResponse(
    Guid Id,
    string Code,
    string Name,
    string Status,
    decimal? AreaHectares,
    GeoJsonPolygonResponse? Boundary,
    long Version);

public sealed record PublishedBaseMapApiResponse(
    Guid Id,
    int VersionNumber,
    Guid SourceSurveyOrderId,
    DateTimeOffset? PublishedAt,
    Guid? PublishedBy,
    uint Version);
