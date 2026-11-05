using AgriDrone.SharedKernel.Application;
using MediatR;
using System.Collections.Generic;

namespace AgriDrone.Modules.Missions.Application.Features.PlantHealth;

// =========================================================
// UC-28: Review Observation (Confirm / Reject)
// =========================================================

public sealed record ReviewPlantObservationCommand(
    Guid FarmId,
    Guid ObservationId,
    string Decision,
    string Reason,
    string? CorrectedConditionCode)
    : IRequest<Result<ReviewPlantObservationResult>>;

public sealed record ReviewPlantObservationResult(
    Guid ObservationId,
    string NewStatus,
    Guid? ResolvedPlantId);

// =========================================================
// UC-29 (GET): Get flagged/pending observations for a farm
// =========================================================

public sealed record GetFlaggedObservationsQuery(
    Guid FarmId)
    : IRequest<Result<GetFlaggedObservationsResult>>;

public sealed record GetFlaggedObservationsResult(
    IReadOnlyList<FlaggedObservationItem> Items);

public sealed record FlaggedObservationItem(
    Guid ObservationId,
    Guid MissionId,
    double? DetectedLat,
    double? DetectedLon,
    double? GpsDistanceMeters,
    double? DetectionConfidence,
    string? MatchingParametersJson,
    Guid? SuggestedPlantId);

// =========================================================
// UC-29 (POST): Resolve Flagged Observation
// =========================================================

public sealed record ResolveFlaggedObservationCommand(
    Guid FarmId,
    Guid MissionId,
    Guid ObservationId,
    string Action,
    Guid? TargetPlantId,
    string? NewPlantCode,
    Guid? ZoneId,
    string Reason)
    : IRequest<Result<ResolveFlaggedObservationResult>>;

public sealed record ResolveFlaggedObservationResult(
    Guid ObservationId,
    string Action,
    string NewStatus,
    Guid? ResolvedPlantId,
    Guid? NewPlantId,
    string? NewPlantCode);

// =========================================================
// UC-30a: Merge Disease Zones
// =========================================================

public sealed record MergeDiseaseZonesCommand(
    Guid FarmId,
    IReadOnlyList<Guid> ZoneIds,
    NetTopologySuite.Geometries.Polygon MergedGeometry)
    : IRequest<Result<MergeDiseaseZonesResult>>;

public sealed record MergeDiseaseZonesResult(
    Guid MergedZoneId,
    int TotalTrees,
    int MergedCount);

// =========================================================
// UC-30b: Update Zone Boundary (Reshape)
// =========================================================

public sealed record UpdateZoneBoundaryCommand(
    Guid FarmId,
    Guid ZoneId,
    NetTopologySuite.Geometries.Polygon NewGeometry)
    : IRequest<Result<UpdateZoneBoundaryResult>>;

public sealed record UpdateZoneBoundaryResult(
    Guid ZoneId,
    string GeometryGeoJson,
    int TotalEnclosedTrees);

// =========================================================
// UC-30c: Update Zone Memberships (Add/Remove plants manually)
// =========================================================

public sealed record UpdateZoneMembershipsCommand(
    Guid FarmId,
    Guid ZoneId,
    IReadOnlyList<Guid> AddPlantIds,
    IReadOnlyList<Guid> RemovePlantIds)
    : IRequest<Result<UpdateZoneMembershipsResult>>;

public sealed record UpdateZoneMembershipsResult(
    Guid ZoneId,
    int TotalEnclosedTrees,
    int AddedCount,
    int RemovedCount);
