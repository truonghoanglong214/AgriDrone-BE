using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Missions.Domain.Telemetry;

public static class TelemetryRouteFactory
{
    public static LineString Create(
        IReadOnlyCollection<MissionTelemetryPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        if (points.Count < 2)
        {
            throw new ArgumentException(
                "At least two telemetry points are required " +
                "to create a flight route.",
                nameof(points));
        }

        var orderedPoints = points
            .OrderBy(point => point.SequenceNumber)
            .ToArray();

        if (orderedPoints
            .GroupBy(point => point.SequenceNumber)
            .Any(group => group.Count() > 1))
        {
            throw new ArgumentException(
                "Telemetry sequence numbers must be unique.",
                nameof(points));
        }

        if (orderedPoints
            .GroupBy(point => point.RecordedAt)
            .Any(group => group.Count() > 1))
        {
            throw new ArgumentException(
                "Telemetry timestamps must be unique.",
                nameof(points));
        }

        for (var index = 1;
             index < orderedPoints.Length;
             index++)
        {
            if (orderedPoints[index].RecordedAt <=
                orderedPoints[index - 1].RecordedAt)
            {
                throw new ArgumentException(
                    "Telemetry timestamps must increase " +
                    "with sequence number.",
                    nameof(points));
            }
        }

        var coordinates = orderedPoints
            .Select(point =>
                new Coordinate(
                    point.Location.X,
                    point.Location.Y))
            .ToArray();

        var geometryFactory =
            NtsGeometryServices.Instance
                .CreateGeometryFactory(srid: 4326);

        return geometryFactory.CreateLineString(
            coordinates);
    }
}