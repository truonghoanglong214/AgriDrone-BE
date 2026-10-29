using AgriDrone.IntegrationContracts.Mapping;

namespace AgriDrone.Modules.Missions.Application.Abstractions.MissionPlanning;

public sealed record MissionScopeZoneSnapshot(
    Guid ZoneId,
    long ZoneVersion,
    GeoJsonPolygonV3 Boundary);
