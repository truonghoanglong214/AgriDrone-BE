using AgriDrone.Modules.Surveys.Domain;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Queries;

public interface ISurveyOrderBoundaryQueries
{
    Task<SurveyOrderBoundaryScopeSnapshot?> GetAsync(
        Guid surveyOrderId,
        CancellationToken cancellationToken = default);
}

public sealed record SurveyOrderBoundaryScopeSnapshot(
    Guid SurveyOrderId,
    string OrderNumber,
    Guid TenantId,
    Guid FarmId,
    SurveyOrderStatus OrderStatus,
    uint OrderVersion,
    bool RequiresBaselineMapping,
    SurveyOrderBoundarySnapshot? CurrentApprovedBoundary,
    IReadOnlyList<SurveyOrderZoneScopeSnapshot> Zones);

public sealed record SurveyOrderBoundarySnapshot(
    Guid FarmBoundaryId,
    int VersionNumber,
    uint Version,
    Polygon Geometry,
    string Source,
    string? EvidenceReference,
    Guid? ReviewedBy,
    DateTimeOffset? ReviewedAt);

public sealed record SurveyOrderZoneScopeSnapshot(
    Guid ZoneId,
    string Code,
    string Name,
    Polygon? Boundary,
    string Status,
    long Version);
