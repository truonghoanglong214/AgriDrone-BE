using AgriDrone.IntegrationContracts.Mapping;

namespace AgriDrone.IntegrationContracts.Surveys;

public sealed record SurveyScopeZoneV3(
    Guid ZoneId,
    long ZoneVersion,
    GeoJsonPolygonV3 Boundary);
