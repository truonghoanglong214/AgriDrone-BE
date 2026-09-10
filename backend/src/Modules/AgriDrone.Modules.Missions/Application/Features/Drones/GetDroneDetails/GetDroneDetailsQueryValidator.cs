using FluentValidation;

namespace AgriDrone.Modules.Missions.Application
    .Features.Drones.GetDroneDetails;

internal sealed class GetDroneDetailsQueryValidator
    : AbstractValidator<GetDroneDetailsQuery>
{
    public GetDroneDetailsQueryValidator()
    {
        RuleFor(query => query.TenantId)
            .NotEmpty();

        RuleFor(query => query.DroneId)
            .NotEmpty();
    }
}