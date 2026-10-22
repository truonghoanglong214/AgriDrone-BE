using FluentValidation;

namespace AgriDrone.Modules.Missions.Application.Features.Missions.RescheduleOrderMission;

internal sealed class RescheduleOrderMissionCommandValidator
    : AbstractValidator<RescheduleOrderMissionCommand>
{
    public RescheduleOrderMissionCommandValidator()
    {
        RuleFor(value => value.FarmId).NotEmpty();
        RuleFor(value => value.MissionId).NotEmpty();
        RuleFor(value => value.ExpectedVersion).GreaterThan(0u);
        RuleFor(value => value.ScheduledDate).Must(value => value != default);
        RuleFor(value => value.ReplacementDroneId)
            .Must(value => value is null || value != Guid.Empty);
    }
}
