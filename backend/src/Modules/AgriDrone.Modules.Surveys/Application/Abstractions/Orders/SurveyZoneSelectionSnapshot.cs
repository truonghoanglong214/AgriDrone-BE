using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Orders;

public sealed record SurveyZoneSelectionSnapshot(
    Guid ZoneId,
    long Version,
    Polygon Boundary);
