using FluentValidation;

namespace AgriDrone.Modules.Missions.Application
    .Features.Drones.GetAvailableDrones;

internal sealed class GetAvailableDronesQueryValidator
    : AbstractValidator<GetAvailableDronesQuery>
{
    public GetAvailableDronesQueryValidator()
    {
        RuleFor(query => query.StartAt)
            .NotEqual(default(DateTimeOffset));

        RuleFor(query => query.EndAt)
            .NotEqual(default(DateTimeOffset));

        RuleFor(query => query)
            .Must(query => query.EndAt > query.StartAt)
            .WithMessage(
                "EndAt must be later than StartAt.");
    }
}