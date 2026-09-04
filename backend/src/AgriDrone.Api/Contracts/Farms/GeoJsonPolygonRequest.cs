using System.ComponentModel.DataAnnotations;

namespace AgriDrone.Api.Contracts.Farms;

public sealed record GeoJsonPolygonRequest(
    string Type,
    double[][][] Coordinates) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (!string.Equals(Type, "Polygon", StringComparison.OrdinalIgnoreCase))
        {
            yield return new ValidationResult(
                "Type must be 'Polygon'.",
                [nameof(Type)]);
        }

        if (Coordinates is null || Coordinates.Length == 0)
        {
            yield return new ValidationResult(
                "Polygon coordinates must contain at least one linear ring.",
                [nameof(Coordinates)]);

            yield break;
        }

        for (var ringIndex = 0; ringIndex < Coordinates.Length; ringIndex++)
        {
            var ring = Coordinates[ringIndex];

            if (ring is null || ring.Length < 4)
            {
                yield return new ValidationResult(
                    $"Polygon ring {ringIndex} must contain at least four positions.",
                    [nameof(Coordinates)]);

                continue;
            }

            var ringIsValid = true;

            for (var positionIndex = 0; positionIndex < ring.Length; positionIndex++)
            {
                var position = ring[positionIndex];

                if (position is null || position.Length != 2)
                {
                    yield return new ValidationResult(
                        $"Position {positionIndex} in polygon ring {ringIndex} must contain exactly longitude and latitude.",
                        [nameof(Coordinates)]);

                    ringIsValid = false;
                    continue;
                }

                var longitude = position[0];
                var latitude = position[1];

                if (!double.IsFinite(longitude) || longitude is < -180 or > 180)
                {
                    yield return new ValidationResult(
                        $"Longitude at position {positionIndex} in polygon ring {ringIndex} must be between -180 and 180.",
                        [nameof(Coordinates)]);

                    ringIsValid = false;
                }

                if (!double.IsFinite(latitude) || latitude is < -90 or > 90)
                {
                    yield return new ValidationResult(
                        $"Latitude at position {positionIndex} in polygon ring {ringIndex} must be between -90 and 90.",
                        [nameof(Coordinates)]);

                    ringIsValid = false;
                }
            }

            if (ringIsValid &&
                (ring[0][0] != ring[^1][0] || ring[0][1] != ring[^1][1]))
            {
                yield return new ValidationResult(
                    $"Polygon ring {ringIndex} must be closed; its first and last positions must be equal.",
                    [nameof(Coordinates)]);
            }
        }
    }
}
