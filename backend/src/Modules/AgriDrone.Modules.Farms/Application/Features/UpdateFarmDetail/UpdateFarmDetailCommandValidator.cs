using FluentValidation;

namespace AgriDrone.Modules.Farms.Application.Features.UpdateFarmDetail;

internal sealed class UpdateFarmDetailCommandValidator
    : AbstractValidator<UpdateFarmDetailCommand>
{
    public UpdateFarmDetailCommandValidator()
    {
        RuleFor(farm => farm.farmId)
            .NotEmpty().WithMessage("Farm id is required.");

        RuleFor(farm => farm.name)
            .NotEmpty().WithMessage("Farm name is required.")
            .MaximumLength(150).WithMessage("Farm name must not exceed 150 characters.");

        RuleFor(farm => farm.address)
            .MaximumLength(200).WithMessage("Address must not exceed 200 characters.");

        RuleFor(farm => farm.areaHectares)
            .GreaterThanOrEqualTo(0)
            .When(farm => farm.areaHectares.HasValue)
            .WithMessage("Farm area must be greater than or equal to zero.");

        RuleFor(farm => farm.boundary)
            .Must(boundary => boundary is null ||
                boundary.SRID == 4326 && boundary.IsValid)
            .WithMessage("Boundary must be a valid Polygon with SRID 4326.");

        RuleFor(farm => farm.centerPoint)
            .Must(point => point is null ||
                point.SRID == 4326 &&
                double.IsFinite(point.X) &&
                double.IsFinite(point.Y) &&
                point.X is >= -180 and <= 180 &&
                point.Y is >= -90 and <= 90)
            .WithMessage("Center point must use SRID 4326 and contain valid longitude and latitude.");

        RuleFor(farm => farm.expectedVersion)
            .GreaterThan(0).WithMessage("Expected version must be greater than zero.");
    }
}
