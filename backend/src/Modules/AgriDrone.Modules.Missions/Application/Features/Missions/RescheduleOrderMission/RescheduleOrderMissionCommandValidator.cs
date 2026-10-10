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
        RuleFor(value => value.StartAt).Must(value => value != default &&
            value.Offset == TimeSpan.Zero);
        RuleFor(value => value.EndAt).Must((request, value) =>
            value.Offset == TimeSpan.Zero && value > request.StartAt);
        RuleFor(value => value.ReplacementDroneId)
            .Must(value => value is null || value != Guid.Empty);
    }
}
