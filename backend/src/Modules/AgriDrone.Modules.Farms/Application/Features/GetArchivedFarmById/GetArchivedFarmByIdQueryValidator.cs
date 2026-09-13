using FluentValidation;

namespace AgriDrone.Modules.Farms.Application.Features.GetArchivedFarmById;

internal sealed class GetArchivedFarmByIdQueryValidator
    : AbstractValidator<GetArchivedFarmByIdQuery>
{
    public GetArchivedFarmByIdQueryValidator()
    {
        RuleFor(query => query.FarmId)
            .NotEmpty()
            .WithMessage("Farm id is required.");
    }
}
