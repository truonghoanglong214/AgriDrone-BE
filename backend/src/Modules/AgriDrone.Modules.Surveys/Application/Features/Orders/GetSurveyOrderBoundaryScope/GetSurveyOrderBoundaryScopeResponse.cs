using AgriDrone.Modules.Surveys.Domain;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Surveys.Application.Features.Orders.GetSurveyOrderBoundaryScope;

public sealed record GetSurveyOrderBoundaryScopeResponse(
    Guid SurveyOrderId,
    string OrderNumber,
    Guid TenantId,
    Guid FarmId,
    SurveyOrderStatus OrderStatus,
    uint OrderVersion,
    bool RequiresBaselineMapping,
    SurveyOrderBoundaryResponse? CurrentApprovedBoundary,
    IReadOnlyList<SurveyOrderZoneScopeResponse> Zones);

public sealed record SurveyOrderBoundaryResponse(
    Guid FarmBoundaryId,
    int VersionNumber,
    uint Version,
    Polygon Geometry,
    string Source,
    string? EvidenceReference,
    Guid? ReviewedBy,
    DateTimeOffset? ReviewedAt);

public sealed record SurveyOrderZoneScopeResponse(
    Guid ZoneId,
    string Code,
    string Name,
    Polygon? Boundary,
    string Status,
    long Version);
