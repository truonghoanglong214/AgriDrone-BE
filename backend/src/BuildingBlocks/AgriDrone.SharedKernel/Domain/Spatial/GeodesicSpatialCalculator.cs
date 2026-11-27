namespace AgriDrone.SharedKernel.Domain.Spatial;

public static class GeodesicSpatialCalculator
{
    private const double EarthRadiusMeters = 6_371_000.0;

    /// <summary>
    /// Calculates the great-circle distance between two WGS84 (SRID 4326) coordinates in meters using the Haversine formula.
    /// Latitude and Longitude are in decimal degrees.
    /// </summary>
    public static double CalculateDistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        var rLat1 = DegreesToRadians(lat1);
        var rLon1 = DegreesToRadians(lon1);
        var rLat2 = DegreesToRadians(lat2);
        var rLon2 = DegreesToRadians(lon2);

        var deltaLat = rLat2 - rLat1;
        var deltaLon = rLon2 - rLon1;

        var a = Math.Sin(deltaLat / 2.0) * Math.Sin(deltaLat / 2.0) +
                Math.Cos(rLat1) * Math.Cos(rLat2) *
                Math.Sin(deltaLon / 2.0) * Math.Sin(deltaLon / 2.0);

        var c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));

        return EarthRadiusMeters * c;
    }

    public static double CalculateInitialBearingDegrees(double lat1, double lon1, double lat2, double lon2)
    {
        var rLat1 = DegreesToRadians(lat1);
        var rLon1 = DegreesToRadians(lon1);
        var rLat2 = DegreesToRadians(lat2);
        var rLon2 = DegreesToRadians(lon2);

        var dLon = rLon2 - rLon1;
        var y = Math.Sin(dLon) * Math.Cos(rLat2);
        var x = Math.Cos(rLat1) * Math.Sin(rLat2) - Math.Sin(rLat1) * Math.Cos(rLat2) * Math.Cos(dLon);
        var initialBearing = Math.Atan2(y, x);
        return (RadiansToDegrees(initialBearing) + 360.0) % 360.0;
    }

    public static double CalculateLateralDistanceMeters(
        double distanceMeters,
        double segmentBearingDegrees,
        double axisBearingDegrees) =>
        distanceMeters * Math.Abs(Math.Sin(DegreesToRadians(segmentBearingDegrees - axisBearingDegrees)));

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
    private static double RadiansToDegrees(double radians) => radians * 180.0 / Math.PI;
}
