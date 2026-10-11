using AgriDrone.Api.Contracts.Farms;

namespace AgriDrone.Api.Contracts.Surveys.Orders;

public sealed record GetSurveyOrderBoundaryScopeApiResponse(
    Guid SurveyOrderId,
    string OrderNumber,
    Guid TenantId,
    Guid FarmId,
    string OrderStatus,
    uint OrderVersion,
    bool RequiresBaselineMapping,
    SurveyOrderBoundaryApiResponse? CurrentApprovedBoundary,
    IReadOnlyList<SurveyOrderZoneScopeApiResponse> Zones);

public sealed record SurveyOrderBoundaryApiResponse(
    Guid FarmBoundaryId,
    int VersionNumber,
    uint Version,
    GeoJsonPolygonResponse Geometry,
    string Source,
    string? EvidenceReference,
    Guid? ReviewedBy,
    DateTimeOffset? ReviewedAt);

public sealed record SurveyOrderZoneScopeApiResponse(
    Guid ZoneId,
    string Code,
    string Name,
    GeoJsonPolygonResponse? Boundary,
    string Status,
    long Version);
