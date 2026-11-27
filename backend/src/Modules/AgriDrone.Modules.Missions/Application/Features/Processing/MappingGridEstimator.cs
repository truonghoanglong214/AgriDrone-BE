using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.SharedKernel.Domain.Spatial;

namespace AgriDrone.Modules.Missions.Application.Features.Processing;

internal static class MappingGridEstimator
{
    private const double RadiansPerDegree = Math.PI / 180.0;
    // This measures agreement on a single axis. Policy tolerances apply during review.
    private const double MinimumAxisAgreement = 0.8;
    private const double ProjectionPrecisionMeters = 0.000001;

    public static (double? BearingDeg, double? RowSpacingM, double? PlantSpacingM) Estimate(
        IReadOnlyList<MissionPlantGpsRow> rows)
    {
        var detected = rows
            .Where(row => string.Equals(row.Status, "DETECTED", StringComparison.OrdinalIgnoreCase))
            .GroupBy(row => (row.Row, row.Col))
            .Where(cell => cell.Count() == 1)
            .Select(cell => cell.Single())
            .ToArray();

        var alongRow = MeasurePairs(detected.GroupBy(row => row.Row), row => row.Col);
        var bearing = EstimateBearing(alongRow);
        if (!bearing.HasValue)
        {
            return default;
        }

        var acrossRows = MeasurePairs(detected.GroupBy(row => row.Col), row => row.Row);
        return (
            bearing,
            Median(acrossRows.Select(pair =>
                GeodesicSpatialCalculator.CalculateLateralDistanceMeters(
                    pair.DistanceMeters, pair.BearingDegrees, bearing.Value) / pair.GridSteps)),
            Median(alongRow.Select(pair => pair.DistanceMeters / pair.GridSteps)));
    }

    private static double? EstimateBearing(IReadOnlyList<GridPair> pairs)
    {
        if (pairs.Count < 2)
        {
            return null;
        }

        var sin = pairs.Sum(pair => Math.Sin(2 * pair.BearingDegrees * RadiansPerDegree));
        var cos = pairs.Sum(pair => Math.Cos(2 * pair.BearingDegrees * RadiansPerDegree));
        if (Math.Sqrt(sin * sin + cos * cos) < pairs.Count * MinimumAxisAgreement)
        {
            return null;
        }

        // Doubling the angles makes opposite directions vote for the same row axis.
        var axis = (Math.Atan2(sin, cos) / (2 * RadiansPerDegree) + 180) % 180;
        var direction = pairs.Sum(pair => Math.Sign(Math.Cos((pair.BearingDegrees - axis) * RadiansPerDegree)));
        return direction < 0 ? axis + 180 : axis;
    }

    private static List<GridPair> MeasurePairs(
        IEnumerable<IGrouping<int, MissionPlantGpsRow>> groups,
        Func<MissionPlantGpsRow, int> getIndex)
    {
        var pairs = new List<GridPair>();
        foreach (var group in groups.OrderBy(group => group.Key))
        {
            var ordered = group.OrderBy(getIndex).ToArray();
            for (var index = 1; index < ordered.Length; index++)
            {
                var from = ordered[index - 1];
                var to = ordered[index];
                var distance = GeodesicSpatialCalculator.CalculateDistanceMeters(
                    (double)from.Latitude, (double)from.Longitude, (double)to.Latitude, (double)to.Longitude);
                if (!double.IsFinite(distance) || distance <= 0)
                {
                    continue;
                }

                pairs.Add(new GridPair(
                    distance,
                    GeodesicSpatialCalculator.CalculateInitialBearingDegrees(
                        (double)from.Latitude, (double)from.Longitude, (double)to.Latitude, (double)to.Longitude),
                    getIndex(to) - getIndex(from)));
            }
        }

        return pairs;
    }

    private static double? Median(IEnumerable<double> distances)
    {
        var sorted = distances.Order().ToArray();
        if (sorted.Length < 2)
        {
            return null;
        }

        var median = (sorted[(sorted.Length - 1) / 2] + sorted[sorted.Length / 2]) / 2;
        return median > ProjectionPrecisionMeters ? median : null;
    }

    private readonly record struct GridPair(double DistanceMeters, double BearingDegrees, int GridSteps);
}
