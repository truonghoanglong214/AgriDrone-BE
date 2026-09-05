using FluentValidation;

namespace AgriDrone.Modules.Farms.Application.Features.GetZonesByFarm;

internal sealed class GetZonesByFarmQueryValidator
    : AbstractValidator<GetZonesByFarmQuery>
{
    public GetZonesByFarmQueryValidator()
    {
        RuleFor(query => query.FarmId)
            .NotEmpty().WithMessage("Farm ID is required.");
    }
}
