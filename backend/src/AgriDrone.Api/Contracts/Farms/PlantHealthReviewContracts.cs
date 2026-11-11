namespace AgriDrone.Api.Contracts.Farms;

// =========================================================
// UC-28: Review Suspected Disease Observation
// =========================================================

public sealed record ReviewObservationRequest(
    string Decision,
    string Reason,
    string? CorrectedConditionCode = null);

public sealed record ReviewObservationResponse(
    Guid ObservationId,
    string NewStatus,
    Guid? ResolvedPlantId,
    string Reason);

// =========================================================
// UC-29: Resolve Flagged Observation
// =========================================================

public sealed record ResolveFlaggedObservationRequest(
    string Action,
    Guid? TargetPlantId = null,
    string? NewPlantCode = null,
    Guid? ZoneId = null,
    string Reason = "Resolved by manager");

public sealed record ResolveFlaggedObservationResponse(
    Guid ObservationId,
    string Action,
    string NewStatus,
    Guid? ResolvedPlantId,
    Guid? NewPlantId,
    string? NewPlantCode);

// =========================================================
// UC-30: Disease Zone Adjustments (Leaflet Draw)
// =========================================================

public sealed record MergeDiseaseZonesRequest(
    IReadOnlyList<Guid> ZoneIds,
    GeoJsonPolygonRequest MergedGeometry);

public sealed record MergeDiseaseZonesResponse(
    Guid MergedZoneId,
    int TotalTrees,
    int MergedZoneCount);

public sealed record UpdateZoneBoundaryRequest(
    GeoJsonPolygonRequest NewGeometry);

public sealed record UpdateZoneBoundaryResponse(
    Guid ZoneId,
    string GeometryGeoJson,
    int TotalEnclosedTrees);

public sealed record UpdateZoneMembershipsRequest(
    IReadOnlyList<Guid> AddPlantIds,
    IReadOnlyList<Guid> RemovePlantIds);

public sealed record UpdateZoneMembershipsResponse(
    Guid ZoneId,
    int TotalEnclosedTrees,
    int AddedCount,
    int RemovedCount);
