using FluentValidation;

namespace AgriDrone.Modules.Farms.Application.Features.GetAssignedFarmOperationalContext;

public sealed class GetAssignedFarmOperationalContextQueryValidator
    : AbstractValidator<GetAssignedFarmOperationalContextQuery>
{
    public GetAssignedFarmOperationalContextQueryValidator()
    {
        RuleFor(x => x.FarmId)
            .NotEmpty()
            .WithMessage("Farm identifier must not be empty.");
    }
}
