using AgriDrone.Modules.Missions.Domain.Missions;
using FluentValidation;

namespace AgriDrone.Modules.Missions.Application
    .Features.Missions.TransitionMission;

internal sealed class TransitionMissionCommandValidator
    : AbstractValidator<TransitionMissionCommand>
{
    public TransitionMissionCommandValidator()
    {
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
                "UC03 only supports InFlight, " +
                "FlightCompleted, FlightFailed and Cancelled.");

        RuleFor(command => command.Reason)
            .NotEmpty()
            .MaximumLength(1000)
            .When(command =>
                command.TargetStatus is
                    MissionStatus.FlightFailed or
                    MissionStatus.Cancelled);

        When(command => command.TargetStatus == MissionStatus.FlightFailed, () =>
        {
            RuleFor(command => command.IncidentOperationId)
                .NotNull().NotEqual(Guid.Empty);
            RuleFor(command => command.IncidentType)
                .Must(value => value is "SIGNAL_LOSS" or "LOW_BATTERY" or
                    "INTERRUPTION" or "FLIGHT_FAILURE");
            RuleFor(command => command.IncidentOutcome)
                .NotEmpty().MaximumLength(1000);
            RuleFor(command => command.RecoveryDecision)
                .Must(value => value is "ABORT" or "RESCHEDULE_REQUIRED");
            RuleFor(command => command.EvidenceReference)
                .NotEmpty().MaximumLength(500);
        });
    }
}
