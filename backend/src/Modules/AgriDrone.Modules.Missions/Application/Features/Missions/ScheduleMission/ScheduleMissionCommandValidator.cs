using FluentValidation;

namespace AgriDrone.Modules.Missions.Application.Features.Missions.ScheduleMission;

internal sealed class ScheduleMissionCommandValidator
    : AbstractValidator<ScheduleMissionCommand>
{
    public ScheduleMissionCommandValidator()
    {
        RuleFor(command => command.FarmId)
            .NotEmpty();

        RuleFor(command => command.MissionId)
            .NotEmpty();

        RuleFor(command => command.ExpectedVersion)
            .GreaterThan(0u);

        RuleFor(command => command.ScheduledDate)
            .Must(date => date != default)
            .WithMessage("ScheduledDate is required.");
    }
}
