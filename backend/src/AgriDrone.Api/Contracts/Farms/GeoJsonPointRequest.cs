using System.ComponentModel.DataAnnotations;

namespace AgriDrone.Api.Contracts.Farms;

public sealed record GeoJsonPointRequest(
    string Type,
    double[] Coordinates) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (!string.Equals(Type, "Point", StringComparison.OrdinalIgnoreCase))
        {
            yield return new ValidationResult(
                "Type must be 'Point'.",
                [nameof(Type)]);
        }

        if (Coordinates is null || Coordinates.Length != 2)
        {
            yield return new ValidationResult(
                "Point coordinates must contain exactly longitude and latitude.",
                [nameof(Coordinates)]);

            yield break;
        }

        var longitude = Coordinates[0];
        var latitude = Coordinates[1];

        if (!double.IsFinite(longitude) || longitude is < -180 or > 180)
        {
            yield return new ValidationResult(
                "Longitude must be between -180 and 180.",
                [nameof(Coordinates)]);
        }

        if (!double.IsFinite(latitude) || latitude is < -90 or > 90)
        {
            yield return new ValidationResult(
                "Latitude must be between -90 and 90.",
                [nameof(Coordinates)]);
        }
    }
}
