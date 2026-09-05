using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Farms.Application.Features.CreateZone
{
    internal sealed class CreateZoneCommandValidator : AbstractValidator<CreateZoneCommand>
    {
        public CreateZoneCommandValidator()
        {
            RuleFor(x => x.FarmId)
                .NotEmpty().WithMessage("FarmId is required.");

            RuleFor(x => x.Code)
                .NotEmpty().WithMessage("Code is required.")
                .MaximumLength(30).WithMessage("Code must not exceed 30 characters.");

            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Name is required.")
                .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");

            RuleFor(x => x.Boundary)
                .Must(boundary => boundary is null ||
                    boundary.SRID == 4326 && boundary.IsValid)
                .WithMessage("Boundary must be a valid Polygon with SRID 4326.");

            RuleFor(x => x.AreaHectares)
                .GreaterThanOrEqualTo(0)
                .When(x => x.AreaHectares.HasValue)
                .WithMessage("AreaHectares must be greater than or equal to 0 if provided.");
        }
    }
}
