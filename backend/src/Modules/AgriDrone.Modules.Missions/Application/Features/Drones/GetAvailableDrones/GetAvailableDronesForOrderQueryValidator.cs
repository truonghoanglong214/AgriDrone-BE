using FluentValidation;

namespace AgriDrone.Modules.Missions.Application.Features.Drones.GetAvailableDrones;

internal sealed class GetAvailableDronesForOrderQueryValidator
    : AbstractValidator<GetAvailableDronesForOrderQuery>
{
    public GetAvailableDronesForOrderQueryValidator()
    {
        RuleFor(query => query.SurveyOrderId).NotEmpty();
    }
}
