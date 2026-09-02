using AgriDrone.Modules.Missions.Domain.Missions;
using FluentValidation;

namespace AgriDrone.Modules.Missions.Application
    .Features.Missions.TransitionMission;

internal sealed class TransitionMissionCommandValidator
    : AbstractValidator<TransitionMissionCommand>
{
    public TransitionMissionCommandValidator()
    {
        RuleFor(command => command.TenantId)
            .NotEmpty();

        RuleFor(command => command.FarmId)
            .NotEmpty();

        RuleFor(command => command.MissionId)
            .NotEmpty();

        RuleFor(command => command.ExpectedVersion)
            .GreaterThan(0u);

        RuleFor(command => command.TargetStatus)
            .Must(status =>
                status is
                    MissionStatus.InFlight or
                    MissionStatus.FlightCompleted or
                    MissionStatus.FlightFailed or
                    MissionStatus.Cancelled)
            .WithMessage(
                "UC02 only supports InFlight, " +
                "FlightCompleted, FlightFailed and Cancelled.");

        RuleFor(command => command.Reason)
            .NotEmpty()
            .MaximumLength(1000)
            .When(command =>
                command.TargetStatus is
                    MissionStatus.FlightFailed or
                    MissionStatus.Cancelled);
    }
}
