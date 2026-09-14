using FluentValidation;

namespace AgriDrone.Modules.Missions.Application
    .Features.Drones.GetSystemDroneDetails;

internal sealed class GetSystemDroneDetailsQueryValidator
    : AbstractValidator<GetSystemDroneDetailsQuery>
{
    public GetSystemDroneDetailsQueryValidator()
    {
        RuleFor(query => query.DroneId)
            .NotEmpty();
    }
}