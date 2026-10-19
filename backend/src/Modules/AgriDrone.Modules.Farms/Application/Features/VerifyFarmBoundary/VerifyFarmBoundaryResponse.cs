using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Farms.Application.Features.VerifyFarmBoundary;

public sealed record VerifyFarmBoundaryResponse(
    Guid BoundaryId,
    Guid FarmId,
    int VersionNumber,
    Polygon Geometry,
    string Status,
    string Source,
    string? EvidenceReference,
    Guid ReviewedBy,
    DateTimeOffset ReviewedAt,
    string ReviewReason,
    uint Version);
