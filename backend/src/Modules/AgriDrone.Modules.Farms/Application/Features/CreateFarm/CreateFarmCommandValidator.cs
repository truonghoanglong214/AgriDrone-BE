using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Farms.Application.Features.CreateFarm
{
    internal sealed class CreateFarmCommandValidator : AbstractValidator<CreateFarmCommand>
    {
        public CreateFarmCommandValidator()
        {

            RuleFor(farm => farm.code)
                .NotEmpty().WithMessage("Farm code is required.")
                .MaximumLength(30).WithMessage("Farm code must not exceed 30 characters.");

            RuleFor(farm => farm.name)
                .NotEmpty().WithMessage("Farm name is required.")
                .MaximumLength(150).WithMessage("Farm name must not exceed 150 characters.");

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
                    point.X is >= -180 and <= 180 &&
                    point.Y is >= -90 and <= 90)
                .WithMessage("Center point must use SRID 4326 and contain valid longitude and latitude.");
        }
    }
}
