using FluentValidation;

namespace AgriDrone.Modules.Farms.Application.Features.GetZoneById;

internal sealed class GetZoneByIdQueryValidator
    : AbstractValidator<GetZoneByIdQuery>
{
    public GetZoneByIdQueryValidator()
    {
        RuleFor(query => query.FarmId)
            .NotEmpty().WithMessage("Farm ID is required.");

        RuleFor(query => query.ZoneId)
            .NotEmpty().WithMessage("Zone ID is required.");
    }
}
